using MassTransit;
using SupportOpsApi.Services;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Consumers;

/// <summary>
/// Sentinel'in <see cref="ServiceRecoveredEvent"/> sinyalini dinler ve açık
/// auto-incident bug'larını "Resolved" durumuna çeker. ServiceIncidentDetector
/// bug yaratma + WS broadcast pipeline'ının diğer yarısı — bu consumer yoksa
/// otomatik bug açılır ama otomatik kapanmaz.
/// </summary>
public sealed class ServiceRecoveredConsumer(
    ServiceIncidentDetector detector,
    ILogger<ServiceRecoveredConsumer> logger) : IConsumer<ServiceRecoveredEvent> {

    public async Task Consume(ConsumeContext<ServiceRecoveredEvent> context) {
        var msg = context.Message;
        if (string.IsNullOrWhiteSpace(msg.TargetService)) {
            return;
        }

        var note = string.IsNullOrWhiteSpace(msg.Note)
            ? $"sentinel: recovered after {msg.ConsecutiveFailingTicks} failing tick(s)"
            : msg.Note;

        logger.LogInformation(
            "[service-recovered] {Target} recovered (after {Ticks} failing ticks) — resolving open auto-bugs",
            msg.TargetService, msg.ConsecutiveFailingTicks);

        await detector.ResolveOpenIncidentsAsync(msg.TargetService, note, context.CancellationToken);
    }
}
