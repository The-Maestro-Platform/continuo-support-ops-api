using SupportOpsApi.Endpoints.Handlers;

namespace SupportOpsApi.Endpoints;

public static class SupportEndpoints {
    public static void MapSupportEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("support").WithOpenApi();

        group.MapGet("/incidents", SupportHandlers.GetIncidents);
        group.MapPost("/intake", SupportHandlers.CreateSupportIntake);
        group.MapPost("/incidents/{id:guid}/actions", SupportHandlers.CreateIncidentAction);
        group.MapPut("/incidents/{id:guid}", SupportHandlers.UpdateIncident).WithOpenApi();

        group.MapGet("/notifications", NotificationHandlers.GetNotifications);
        group.MapPost("/notifications/{id:guid}/read", NotificationHandlers.MarkNotificationRead);
        group.MapPost("/notifications/{id:guid}/respond", NotificationHandlers.RespondNotification);

        group.MapGet("/alerts", SupportHandlers.GetAlerts);
        group.MapGet("/knowledge", SupportHandlers.GetKnowledgeBase);
        group.MapGet("/shifts", SupportHandlers.GetShifts);
    }
}

public record UpdateIncidentRequest(string? Status, string? Owner);
public record SupportIntakeRequest(string Topic, string? Branch, string Summary);
public record SupportIntakeResponse(Guid IncidentId, Guid WorkItemId);
