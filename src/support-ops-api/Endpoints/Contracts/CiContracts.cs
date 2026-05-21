namespace SupportOpsApi.Endpoints.Contracts;

// ---------- CI Runs ----------

/// <summary>
/// Gitea Actions workflow'u tarafından push event'inde POST'lanır. Yeni bir CI run kaydı oluşturup
/// id'yi döner; workflow bu id'yi sonraki update'lerde kullanır.
/// </summary>
public record StartCiRunRequest(
    string CommitSha,
    string Branch,
    string? Author,
    string? AuthorEmail,
    string? CommitMessage,
    IEnumerable<string>? ChangedFiles,
    IEnumerable<string>? ServicesInScope,
    string? WorkflowRunUrl,
    string? RunnerName,
    string? EnvironmentScope);

public record StartCiRunResponse(Guid RunId);

/// <summary>
/// Workflow run'ı bitirip sonuçları toparladıktan sonra PATCH'ler. Status: passed/failed/error.
/// Project breakdown ayrı bir endpoint'le (`POST /ci/runs/{id}/projects`) bildirilir.
/// </summary>
public record FinishCiRunRequest(
    string Status,
    int TotalProjects,
    int PassedProjects,
    int FailedProjects,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    int DurationMs,
    string? ErrorMessage);

/// <summary>Workflow her bir test projesi tamamlandığında bir CiTestProjectResult bildirir.</summary>
public record ReportCiProjectRequest(
    string ProjectName,
    string ServiceName,
    string Status,
    int Passed,
    int Failed,
    int Skipped,
    int DurationMs,
    IEnumerable<CiTestFailure>? Failures);

public record CiTestFailure(string TestName, string? Message);

// ---------- DTOs (UI + deploy gate) ----------

public record CiRunDto(
    Guid Id,
    string CommitSha,
    string Branch,
    string Author,
    string? AuthorEmail,
    string? CommitMessage,
    string[] ServicesInScope,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt,
    int DurationMs,
    int TotalProjects,
    int PassedProjects,
    int FailedProjects,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    string? WorkflowRunUrl,
    string? RunnerName,
    string EnvironmentScope,
    string? ErrorMessage);

public record CiTestProjectResultDto(
    Guid Id,
    Guid RunId,
    string ProjectName,
    string ServiceName,
    string Status,
    int Passed,
    int Failed,
    int Skipped,
    int DurationMs,
    string? FailureDetailsJson,
    DateTimeOffset StartedAt,
    DateTimeOffset? FinishedAt);

public record CiRunDetailDto(CiRunDto Run, CiTestProjectResultDto[] Projects);

/// <summary>
/// Deploy gate — auto-deploy worker bir servisi redeploy etmeden önce bu endpoint'e
/// gelir; en son test sonucu `failed` ise deploy atlanır. `null` durumunda (henüz CI
/// koşmamış) deploy'a izin verilir — testler bloke etmez, fail bloklar.
/// </summary>
public record ServiceCiStatusDto(
    string ServiceName,
    string? LastStatus,        // passed | failed | error | running | null
    string? LastCommitSha,
    string? LastBranch,
    DateTimeOffset? LastRunAt,
    int LastFailed,
    int LastPassed,
    string? LastWorkflowRunUrl);

/// <summary>
/// Auto-deploy worker, CI gate'in `failed` durumunda atladığı servisleri burada bildirir.
/// Support-ops-api UI'a WebSocket notification publish eder + ileride mail/Slack için
/// queue'ya yazılabilir. Idempotent değil — her tick patladığında tekrar gönderilir
/// (rate-limit auto-deploy worker tarafında handled).
/// </summary>
public record DeployBlockedNotifyRequest(
    string Environment,
    IEnumerable<DeployBlockedServiceDto> BlockedServices);

public record DeployBlockedServiceDto(
    string ServiceName,
    string? CommitSha,
    int FailedTests,
    string? WorkflowRunUrl);
