using SupportOpsApi.Endpoints.Handlers;

namespace SupportOpsApi.Endpoints;

public static class SeleniumEndpoints {
    public static void MapSeleniumEndpoints(this IEndpointRouteBuilder app) {
        var tests = app.MapGroup("selenium/tests").WithOpenApi();
        tests.MapGet("/", SeleniumHandlers.GetTests);
        tests.MapGet("/{id:guid}", SeleniumHandlers.GetTestById);
        tests.MapPost("/", SeleniumHandlers.CreateTest);
        tests.MapPut("/{id:guid}", SeleniumHandlers.UpdateTest);
        tests.MapDelete("/{id:guid}", SeleniumHandlers.DeleteTest);

        var flows = app.MapGroup("selenium/flows").WithOpenApi();
        flows.MapGet("/", SeleniumHandlers.GetFlows);
        flows.MapPost("/", SeleniumHandlers.CreateFlow);
        flows.MapPut("/{id:guid}", SeleniumHandlers.UpdateFlow);
        flows.MapDelete("/{id:guid}", SeleniumHandlers.DeleteFlow);

        var runs = app.MapGroup("selenium/runs").WithOpenApi();
        runs.MapGet("/", SeleniumHandlers.GetRuns);
        runs.MapPost("/", SeleniumHandlers.QueueRun);
        runs.MapGet("/{id:guid}/log", SeleniumHandlers.GetRunLog);
        runs.MapGet("/{id:guid}/steps", SeleniumHandlers.GetRunSteps);
        runs.MapPost("/{id:guid}/steps", SeleniumHandlers.ReportRunSteps);
        runs.MapPost("/{id:guid}/cancel", SeleniumHandlers.CancelRun);
        // Lightweight status poll — runners hit this every few seconds while executing so they can
        // kill the in-flight `dotnet test` process when a UI-driven cancel lands in the DB.
        runs.MapGet("/{id:guid}/status", SeleniumHandlers.GetRunStatus);
        // Runner-facing: runners call this to stream status updates for the run they own.
        runs.MapPost("/{id:guid}/status", SeleniumHandlers.UpdateRunStatus);

        var runners = app.MapGroup("selenium/runners").WithOpenApi();
        runners.MapGet("/", SeleniumHandlers.GetRunners);
        runners.MapPost("/register", SeleniumHandlers.RegisterRunner);
        runners.MapPatch("/{id:guid}", SeleniumHandlers.UpdateRunner);
        runners.MapPost("/{id:guid}/heartbeat", SeleniumHandlers.Heartbeat);
        runners.MapPost("/{id:guid}/claim", SeleniumHandlers.ClaimNextRun);
    }
}
