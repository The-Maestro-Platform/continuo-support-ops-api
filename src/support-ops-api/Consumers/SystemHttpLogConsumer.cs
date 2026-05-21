using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using SupportOpsApi.Services;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Consumers;

public sealed class SystemHttpLogConsumer(SupportOpsDbContext db, ServiceIncidentDetector detector, ILogger<SystemHttpLogConsumer> logger) : IConsumer<SystemHttpLogEvent> {
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task Consume(ConsumeContext<SystemHttpLogEvent> context) {
        var msg = context.Message;

        // 2026-05-19: Idempotency guard (AGENTS.md §13.17 — multi-replica race protection).
        // MassTransit redelivery (consumer crash + nack) + 2 replika ile aynı msg.Id'nin
        // iki kez Insert edilmesi mümkün. PK üzerinde unique check + DbUpdateException
        // catch ile guard'la. Race tipik MassTransit retry semantiğinde gözlemlenir;
        // burada uniqueness PK constraint'iyle DB-side garantili olduğu için catch yeterli.
        var alreadyExists = await db.SystemHttpLogs
            .AsNoTracking()
            .AnyAsync(x => x.Id == msg.Id, context.CancellationToken);
        if (alreadyExists) {
            return;
        }

        var entity = new SystemHttpLogEntry {
            Id = msg.Id,
            OccurredAtUtc = msg.OccurredAtUtc,
            Service = msg.Service ?? string.Empty,
            Direction = msg.Direction ?? string.Empty,
            Method = msg.Method ?? string.Empty,
            Path = msg.Path ?? string.Empty,
            StatusCode = msg.StatusCode,
            DurationMs = msg.DurationMs,
            TenantId = msg.TenantId,
            ClientApp = msg.ClientApp,
            RemoteIp = msg.RemoteIp,
            ClientIp = msg.ClientIp,
            UserAgent = Trim(msg.UserAgent, 512),
            InitiatorUserId = Trim(msg.InitiatorUserId, 128),
            InitiatorUserName = Trim(msg.InitiatorUserName, 200),
            InitiatorUserEmail = Trim(msg.InitiatorUserEmail, 256),
            ClientGeoCountryCode = Trim(msg.Geo?.CountryCode, 8),
            ClientGeoRegion = Trim(msg.Geo?.Region, 120),
            ClientGeoCity = Trim(msg.Geo?.City, 120),
            ClientGeoLatitude = msg.Geo?.Latitude,
            ClientGeoLongitude = msg.Geo?.Longitude,
            TargetService = msg.TargetService,
            TargetUrl = msg.TargetUrl,
            TraceId = msg.TraceId,
            SpanId = msg.SpanId,
            CorrelationId = msg.CorrelationId,
            RequestHeadersJson = msg.RequestHeaders == null ? null : JsonSerializer.Serialize(msg.RequestHeaders, Json),
            ResponseHeadersJson = msg.ResponseHeaders == null ? null : JsonSerializer.Serialize(msg.ResponseHeaders, Json),
            RequestBody = Trim(msg.RequestBody, 64 * 1024),
            ResponseBody = Trim(msg.ResponseBody, 64 * 1024),
            Error = Trim(msg.Error, 32 * 1024)
        };

        await db.SystemHttpLogs.AddAsync(entity, context.CancellationToken);
        try {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
            // Race: AnyAsync sonrası ama SaveChanges öncesi başka replika insert yapmış.
            // Idempotent skip — duplicate log ekleme.
            logger.LogDebug(ex, "SystemHttpLog {Id} duplicate insert raced, skipping", msg.Id);
            return;
        }

        if (ServiceIncidentDetector.IsFailure(msg)) {
            await detector.HandleAsync(msg, context.CancellationToken);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) {
        // Postgres: 23505 unique_violation. MSSQL: 2627/2601.
        var inner = ex.InnerException;
        var msg = inner?.Message ?? ex.Message;
        return msg.Contains("23505", StringComparison.Ordinal)
            || msg.Contains("unique constraint", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("duplicate key", StringComparison.OrdinalIgnoreCase)
            || msg.Contains("Cannot insert duplicate", StringComparison.OrdinalIgnoreCase);
    }

    private static string? Trim(string? value, int maxLen) {
        if (string.IsNullOrEmpty(value)) {
            return value;
        }

        if (maxLen <= 0) {
            return null;
        }

        return value.Length <= maxLen ? value : value[..maxLen];
    }
}
