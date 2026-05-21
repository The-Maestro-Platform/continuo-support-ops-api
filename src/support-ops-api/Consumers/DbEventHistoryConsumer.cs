using MassTransit;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using Continuo.Shared.Contracts;

namespace SupportOpsApi.Consumers;

public sealed class DbEventHistoryConsumer(SupportOpsDbContext db) : IConsumer<DbEventHistoryEvent> {
    public async Task Consume(ConsumeContext<DbEventHistoryEvent> context) {
        var msg = context.Message;
        var entity = new DbEventHistoryEntry {
            Id = msg.Id,
            OccurredAtUtc = msg.OccurredAtUtc,
            Service = msg.Service,
            DbContext = msg.DbContext,
            Database = msg.Database,
            AddedCount = msg.AddedCount,
            ModifiedCount = msg.ModifiedCount,
            DeletedCount = msg.DeletedCount,
            DurationMs = msg.DurationMs,
            CorrelationId = msg.CorrelationId,
            TraceId = msg.TraceId,
            SpanId = msg.SpanId,
            InitiatorUserId = msg.InitiatorUserId,
            InitiatorUserName = msg.InitiatorUserName,
            InitiatorUserEmail = msg.InitiatorUserEmail,
            EntitiesJson = msg.EntitiesJson,
            Error = msg.Error
        };

        await db.DbEventHistory.AddAsync(entity, context.CancellationToken);
        await db.SaveChangesAsync(context.CancellationToken);
    }
}
