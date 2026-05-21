using SupportOpsApi.Endpoints.Handlers;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints;

public static class SystemLogEndpoints {
    public static void MapSystemLogEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("support/logs").WithOpenApi();

        group.MapGet("/http", SystemLogHandlers.ListSystemHttpLogs);
        group.MapGet("/http/{id:guid}", SystemLogHandlers.GetSystemHttpLogById);
        group.MapGet("/app", SystemLogHandlers.ListSystemAppLogs);
        group.MapGet("/app/{id:guid}", SystemLogHandlers.GetSystemAppLogById);
        group.MapGet("/robot-flow", SystemLogHandlers.ListRobotFlow);
        group.MapGet("/db-events", SystemLogHandlers.ListDbEventHistory);
        group.MapGet("/db-events/{id:guid}", SystemLogHandlers.GetDbEventHistoryById);
        group.MapGet("/ui", SystemLogHandlers.ListUiErrorLogs).WithMetadata(new ContinuoProxyMethodAttribute("ui"));
        group.MapGet("/ui/{id:guid}", SystemLogHandlers.GetUiErrorLogById).WithMetadata(new ContinuoProxyMethodAttribute("ui"));
        group.MapPost("/ui", SystemLogHandlers.IngestUiErrorLogs).WithMetadata(new ContinuoProxyMethodAttribute("ui"));
    }
}
