using SupportOpsApi.Endpoints.Handlers;

namespace SupportOpsApi.Endpoints;

public static class PilotFeedbackEndpoints {
    public static void MapPilotFeedbackEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("support/pilot-feedback").WithOpenApi();

        group.MapGet("/", PilotFeedbackHandlers.ListFeedback);
        group.MapGet("/{id:guid}", PilotFeedbackHandlers.GetFeedback);
        group.MapPost("/", PilotFeedbackHandlers.CreateFeedback);
        group.MapPut("/{id:guid}", PilotFeedbackHandlers.UpdateFeedback);
    }
}
