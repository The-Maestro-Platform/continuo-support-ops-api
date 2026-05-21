using System.Net.Http.Json;
using System.Text.Json;

namespace SupportOpsApi.Services;

/// <summary>
/// Forwards terminal Selenium run results to <c>devops-reporting-api</c> so the dev
/// console's Pipeline Health surface reflects activity from the live runner queue,
/// not just GitHub Actions. The reporting service stores its own
/// <c>devops.SeleniumRuns</c> table; without this bridge those rows would only ever
/// be populated by <c>scripts/deploy/publish-devops-report.ps1</c> from CI, which leaves
/// on-prem and srv01 deployments showing zero runs.
/// </summary>
public sealed partial class DevOpsReportingClient {
    private readonly HttpClient _http;
    private readonly ILogger<DevOpsReportingClient> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug, Message = "[devops-reporting] selenium publish skipped run={RunId} status={Status}")]
    private partial void LogPublishSkipped(Guid runId, string status);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning, Message = "[devops-reporting] selenium publish failed run={RunId}")]
    private partial void LogPublishFailed(Exception ex, Guid runId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Debug, Message = "[devops-reporting] unit publish skipped run={RunId} status={Status}")]
    private partial void LogUnitPublishSkipped(Guid runId, string status);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "[devops-reporting] unit publish failed run={RunId}")]
    private partial void LogUnitPublishFailed(Exception ex, Guid runId);

    public DevOpsReportingClient(HttpClient http, ILogger<DevOpsReportingClient> logger) {
        _http = http;
        _logger = logger;
        _http.DefaultRequestHeaders.Add("X-Client-App", "support-ops-api");
        _http.Timeout = TimeSpan.FromSeconds(15);
    }

    /// <summary>
    /// Mirrors a finished selenium run to devops-reporting-api. Best-effort: failures
    /// are swallowed so they never block the runner status update path.
    /// </summary>
    public async Task PublishSeleniumRunAsync(SeleniumRunReport report, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(report.Status)) {
            LogPublishSkipped(report.RunId, "(empty)");
            return;
        }

        try {
            var payload = new {
                suite = report.Suite,
                branch = report.Branch,
                buildId = report.BuildId,
                environment = report.Environment,
                status = report.Status,
                durationSeconds = report.DurationSeconds,
                startedAt = report.StartedAt,
                steps = report.Steps?.Select((s, idx) => new {
                    title = s.Title,
                    status = s.Status,
                    screenshotUrl = s.ScreenshotUrl,
                    consoleLog = s.ConsoleLog,
                    order = idx + 1
                })
            };

            var response = await _http.PostAsJsonAsync("/reporting/selenium/runs", payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("[devops-reporting] selenium publish non-success run={RunId} status={Status}",
                    report.RunId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) {
            LogPublishFailed(ex, report.RunId);
        }
    }

    /// <summary>
    /// Mirrors a finished CI unit-test run to devops-reporting-api's <c>/unit-tests/runs</c>.
    /// Best-effort: failures are swallowed so they never block the CI FinishRun handler.
    /// </summary>
    public async Task PublishUnitTestRunAsync(UnitTestRunReport report, CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(report.Status)) {
            LogUnitPublishSkipped(report.RunId, "(empty)");
            return;
        }

        try {
            var payload = new {
                pipeline = report.Pipeline,
                commit = report.Commit,
                status = report.Status,
                durationSeconds = report.DurationSeconds,
                completedAt = report.CompletedAt,
                testCases = report.TestCases?.Select(c => new {
                    name = c.Name,
                    status = c.Status,
                    durationSeconds = c.DurationSeconds,
                    failureMessage = c.FailureMessage
                })
            };

            var response = await _http.PostAsJsonAsync("/unit-tests/runs", payload, JsonOptions, ct);
            if (!response.IsSuccessStatusCode) {
                _logger.LogWarning("[devops-reporting] unit publish non-success run={RunId} status={Status}",
                    report.RunId, (int)response.StatusCode);
            }
        }
        catch (Exception ex) {
            LogUnitPublishFailed(ex, report.RunId);
        }
    }
}

public sealed record SeleniumRunReport(
    Guid RunId,
    string Suite,
    string Branch,
    string BuildId,
    string Environment,
    string Status,
    double DurationSeconds,
    DateTimeOffset StartedAt,
    IReadOnlyList<SeleniumRunReportStep>? Steps);

public sealed record SeleniumRunReportStep(
    string Title,
    string Status,
    string? ScreenshotUrl,
    string? ConsoleLog);

/// <summary>
/// Finished CI unit-test run payload that mirrors into <c>devops-reporting-api</c>'s
/// <c>UnitTestRuns</c> table — without this bridge Pipeline Health'in "Unit test runs"
/// tablosu hep boş çünkü Gitea Actions workflow'u sadece support-ops-api'nin
/// <c>/ci/runs</c> endpoint'ine yazıyor.
/// </summary>
public sealed record UnitTestRunReport(
    Guid RunId,
    string Pipeline,
    string Commit,
    string Status,
    double DurationSeconds,
    DateTimeOffset CompletedAt,
    IReadOnlyList<UnitTestRunReportCase>? TestCases);

public sealed record UnitTestRunReportCase(
    string Name,
    string Status,
    double DurationSeconds,
    string? FailureMessage);
