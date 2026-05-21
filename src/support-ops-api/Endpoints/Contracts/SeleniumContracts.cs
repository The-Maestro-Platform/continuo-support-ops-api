namespace SupportOpsApi.Endpoints.Contracts;

// ---------- Catalog ----------

public record SeleniumTestDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    string Kind,
    string? CodeFullyQualifiedName,
    string? ScenarioJson,
    string[] Tags,
    int TimeoutSeconds,
    bool IsActive,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public record UpsertSeleniumTestRequest(
    string Code,
    string Name,
    string? Description,
    string Kind,
    string? CodeFullyQualifiedName,
    string? ScenarioJson,
    IEnumerable<string>? Tags,
    int? TimeoutSeconds,
    bool? IsActive);

// ---------- Flows ----------

public record SeleniumFlowStepDto(
    Guid Id,
    int Order,
    Guid TestId,
    string? TestCode,
    string? TestName,
    bool StopOnFailure);

public record SeleniumFlowDto(
    Guid Id,
    string Code,
    string Name,
    string? Description,
    bool IsActive,
    string CreatedBy,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SeleniumFlowStepDto[] Steps);

public record UpsertSeleniumFlowStepRequest(
    int Order,
    Guid TestId,
    bool StopOnFailure);

public record UpsertSeleniumFlowRequest(
    string Code,
    string Name,
    string? Description,
    bool? IsActive,
    IEnumerable<UpsertSeleniumFlowStepRequest> Steps);

// ---------- Runs (queue) ----------

public record SeleniumRunDto(
    Guid Id,
    Guid TestId,
    string? TestCode,
    string? TestName,
    Guid? FlowId,
    string? FlowCode,
    int? FlowStepOrder,
    Guid? FlowBatchId,
    Guid? RunnerId,
    string? RunnerName,
    string Status,
    int Priority,
    int AttemptCount,
    string QueuedBy,
    DateTimeOffset QueuedAt,
    DateTimeOffset? ClaimedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int DurationMs,
    string? FailureMessage,
    string? ScreenshotRef,
    Guid? AutoBugWorkItemId);

public record SeleniumRunLogDto(Guid Id, string? Stdout, string? Stderr);

public record SeleniumRunStatusDto(Guid Id, string Status);

public record QueueRunRequest(
    Guid? TestId,
    Guid? FlowId,
    int? Priority,
    Dictionary<string, string>? Environment,
    Guid? TargetRunnerId,
    string? EnvironmentScope);

// ---------- Runner protocol ----------

public record RegisterRunnerRequest(
    string Name,
    string Location,
    string? MachineName,
    string? Version,
    IEnumerable<string>? Capabilities,
    int? MaxParallel,
    string? Environment);

public record RegisterRunnerResponse(Guid RunnerId, int MaxParallel);

public record UpdateRunnerRequest(int? MaxParallel);

public record ClaimRunResponse(
    Guid RunId,
    Guid TestId,
    string TestKind,
    string TestCode,
    string TestName,
    string? CodeFullyQualifiedName,
    string? ScenarioJson,
    int TimeoutSeconds,
    string? EnvironmentJson);

public record RunStatusUpdateRequest(
    string Status,
    string? Stdout,
    string? Stderr,
    string? FailureMessage,
    string? ScreenshotRef,
    int? DurationMs,
    IEnumerable<RunStepPayload>? Steps);

public record RunStepPayload(
    int Order,
    string Title,
    string Status,
    int? DurationMs,
    string? ErrorMessage,
    string? ScreenshotRef,
    string? ConsoleLog);

public record SeleniumRunStepDto(
    Guid Id,
    Guid RunId,
    int Order,
    string Title,
    string Status,
    int DurationMs,
    string? ErrorMessage,
    string? ScreenshotRef,
    string? ConsoleLog,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt);

// ---------- Runners list (UI) ----------

public record SeleniumRunnerDto(
    Guid Id,
    string Name,
    string Location,
    string MachineName,
    string Version,
    string Status,
    string[] Capabilities,
    Guid? CurrentRunId,
    int MaxParallel,
    string Environment,
    DateTimeOffset LastHeartbeatAt,
    DateTimeOffset CreatedAt);
