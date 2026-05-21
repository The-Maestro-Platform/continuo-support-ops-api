using System.Text.Json;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Consumers;

public sealed class SupportOpsLogRequestConsumer(
    SupportOpsDbContext db,
    ILogger<SupportOpsLogRequestConsumer> logger)
    : IConsumer<SupportOpsLogRequest> {
    public async Task Consume(ConsumeContext<SupportOpsLogRequest> context) {
        var logId = context.Message.LogId;
        var item = await db.SystemHttpLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == logId, context.CancellationToken);
        if (item is null) {
            if (logger.IsEnabled(LogLevel.Information)) {
                logger.LogInformation("Support-ops log request not found. LogId={LogId}", logId);
            }
            await context.RespondAsync(new SupportOpsLogResponse(logId, false, null, "Log not found."));
            return;
        }

        var payload = new {
            item.Id,
            item.OccurredAtUtc,
            item.Service,
            item.Direction,
            item.Method,
            item.Path,
            item.StatusCode,
            item.DurationMs,
            item.TenantId,
            item.ClientApp,
            item.RemoteIp,
            item.ClientIp,
            item.InitiatorUserId,
            item.InitiatorUserName,
            item.InitiatorUserEmail,
            item.ClientGeoCountryCode,
            item.ClientGeoRegion,
            item.ClientGeoCity,
            item.ClientGeoLatitude,
            item.ClientGeoLongitude,
            item.UserAgent,
            item.TargetService,
            item.TargetUrl,
            item.TraceId,
            item.SpanId,
            item.CorrelationId,
            item.RequestHeadersJson,
            item.ResponseHeadersJson,
            item.RequestBody,
            item.ResponseBody,
            item.Error
        };

        var json = JsonSerializer.Serialize(payload);
        if (logger.IsEnabled(LogLevel.Information)) {
            logger.LogInformation("Support-ops log request served. LogId={LogId} PayloadLength={PayloadLength}", logId, json.Length);
        }
        await context.RespondAsync(new SupportOpsLogResponse(logId, true, json, null));
    }
}
