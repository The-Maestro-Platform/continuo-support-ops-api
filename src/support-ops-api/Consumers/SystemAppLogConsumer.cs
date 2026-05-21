using MassTransit;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Consumers;

public sealed class SystemAppLogConsumer(SupportOpsDbContext db, ILogger<SystemAppLogConsumer> logger) : IConsumer<SystemAppLogEvent> {
    public async Task Consume(ConsumeContext<SystemAppLogEvent> context) {
        var msg = context.Message;

        // Idempotency — see SystemHttpLogConsumer for the rationale (AGENTS.md §13.17,
        // multi-replica retry race). Same shape: PK check first, DbUpdateException
        // catch for the racy second-writer path.
        var alreadyExists = await db.SystemAppLogs
            .AsNoTracking()
            .AnyAsync(x => x.Id == msg.Id, context.CancellationToken);
        if (alreadyExists) {
            return;
        }

        var entity = new SystemAppLogEntry {
            Id = msg.Id,
            OccurredAtUtc = msg.OccurredAtUtc,
            Service = Trim(msg.Service, 120) ?? string.Empty,
            Level = Trim(msg.Level, 24) ?? string.Empty,
            Message = Trim(msg.Message, 16 * 1024) ?? string.Empty,
            MessageTemplate = Trim(msg.MessageTemplate, 16 * 1024),
            SourceContext = Trim(msg.SourceContext, 256),
            Exception = Trim(msg.Exception, 32 * 1024),
            CorrelationId = Trim(msg.CorrelationId, 128),
            TraceId = Trim(msg.TraceId, 64),
            SpanId = Trim(msg.SpanId, 32),
            TenantId = Trim(msg.TenantId, 64),
            UserName = Trim(msg.UserName, 200),
            PropertiesJson = msg.PropertiesJson
        };

        await db.SystemAppLogs.AddAsync(entity, context.CancellationToken);
        try {
            await db.SaveChangesAsync(context.CancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex)) {
            logger.LogDebug(ex, "SystemAppLog {Id} duplicate insert raced, skipping", msg.Id);
        }
    }

    private static bool IsUniqueViolation(DbUpdateException ex) {
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
