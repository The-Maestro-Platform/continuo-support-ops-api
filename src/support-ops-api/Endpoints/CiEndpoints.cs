using SupportOpsApi.Endpoints.Handlers;

namespace SupportOpsApi.Endpoints;

public static class CiEndpoints {
    public static void MapCiEndpoints(this IEndpointRouteBuilder app) {
        // Workflow-facing endpoints (Gitea Actions M2M auth ile çağırır).
        var runs = app.MapGroup("ci/runs").WithOpenApi();
        runs.MapPost("/", CiHandlers.StartRun);
        runs.MapPatch("/{id:guid}", CiHandlers.FinishRun);
        runs.MapPost("/{id:guid}/projects", CiHandlers.ReportProject);

        // UI-facing list / detail.
        runs.MapGet("/", CiHandlers.GetRuns);
        runs.MapGet("/{id:guid}", CiHandlers.GetRunDetail);

        // Deploy gate — PushDrivenAutoDeployWorker tek servisin son sonucunu sorar.
        var services = app.MapGroup("ci/services").WithOpenApi();
        services.MapGet("/{service}/latest", CiHandlers.GetServiceStatus);

        // CI gate'in atladığı servisler için notification dispatch (auto-deploy worker çağırır).
        var notify = app.MapGroup("ci/notify").WithOpenApi();
        notify.MapPost("/deploy-blocked", CiHandlers.NotifyDeployBlocked);
    }
}
