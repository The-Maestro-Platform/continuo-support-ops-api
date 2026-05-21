using SupportOpsApi.Endpoints.Handlers;

namespace SupportOpsApi.Endpoints;

public static class WorkItemEndpoints {
    public static void MapWorkItemEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("work-items").WithOpenApi();

        group.MapGet("/", WorkItemHandlers.GetWorkItems);
        group.MapGet("/{id:guid}", WorkItemHandlers.GetWorkItemById);
        group.MapPost("/", WorkItemHandlers.CreateWorkItem);
        group.MapPost("/auto-bug", WorkItemHandlers.CreateAutoBug);
        group.MapPut("/{id:guid}", WorkItemHandlers.UpdateWorkItem);
        group.MapDelete("/{id:guid}", WorkItemHandlers.DeleteWorkItem);
        group.MapPost("/{id:guid}/links", WorkItemHandlers.CreateWorkItemLink);
        group.MapDelete("/{id:guid}/links/{linkId:guid}", WorkItemHandlers.DeleteWorkItemLink);
        group.MapPost("/{id:guid}/comments", WorkItemHandlers.AddWorkItemComment);
        group.MapPost("/{id:guid}/attachments", WorkItemHandlers.UploadWorkItemAttachment);
        group.MapGet("/files/{id:guid}", WorkItemHandlers.DownloadFile);
    }

    public static void MapDocumentEndpoints(this IEndpointRouteBuilder app) {
        var group = app.MapGroup("documents").WithOpenApi();

        group.MapGet("/", WorkItemHandlers.GetDocuments);
        group.MapGet("/{id:guid}", WorkItemHandlers.GetDocumentById);
        group.MapPost("/", WorkItemHandlers.CreateDocument);
        group.MapPut("/{id:guid}", WorkItemHandlers.UpdateDocument);
        group.MapDelete("/{id:guid}", WorkItemHandlers.DeleteDocument);
    }
}
