namespace SupportOpsApi.Endpoints.Contracts;

/// <summary>
/// Payload pushed by automated test runners (Selenium E2E, smoke checks) when a scenario
/// fails. Triggers the auto-bug flow that creates a work item, attaches a comment with the
/// failure context and fires an in-app notification for the first available analyst.
/// </summary>
public record AutoBugRequest(
    string Title,
    string? TestName,
    string? Source,
    string? Summary,
    string? StackTrace,
    string? LogTail,
    string? ScreenshotRef,
    string? Assignee,
    string? Priority,
    IEnumerable<string>? Tags);

public record AutoBugResponse(
    Guid WorkItemId,
    Guid NotificationId,
    string Assignee);
