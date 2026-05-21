using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;
using SupportOpsApi.Services;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints.Handlers;

/// <summary>
/// Gitea Actions workflow'larından gelen test sonuçlarını saklar ve auto-deploy worker'ın
/// deploy gate sorgularına cevap verir. Selenium run'larından farkı: bu runner-poll'lu değil,
/// pasif kayıt katmanı; Gitea Actions çalıştırıyor, biz sadece store + UI surface ediyoruz.
/// </summary>
public static class CiHandlers {
    public static async Task<Ok<StartCiRunResponse>> StartRun(
        StartCiRunRequest request,
        TaskFlowDbContext db,
        NotificationHub hub,
        CancellationToken ct) {
        var run = new CiRun {
            CommitSha = (request.CommitSha ?? string.Empty).Trim(),
            Branch = (request.Branch ?? string.Empty).Trim(),
            Author = (request.Author ?? string.Empty).Trim(),
            AuthorEmail = request.AuthorEmail?.Trim(),
            CommitMessage = TruncateMessage(request.CommitMessage),
            ChangedFilesJson = SerializeList(request.ChangedFiles),
            ServicesInScope = string.Join(",", (request.ServicesInScope ?? Array.Empty<string>())
                .Where(s => !string.IsNullOrWhiteSpace(s))
                .Select(s => s.Trim().ToLowerInvariant())
                .Distinct()),
            Status = "running",
            StartedAt = DateTimeOffset.UtcNow,
            WorkflowRunUrl = request.WorkflowRunUrl?.Trim(),
            RunnerName = request.RunnerName?.Trim(),
            EnvironmentScope = NormalizeEnv(request.EnvironmentScope)
        };
        db.CiRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await BroadcastAsync(hub, "ci-run-started", $"CI run started: {run.CommitSha[..Math.Min(10, run.CommitSha.Length)]} ({run.ServicesInScope})", ct);
        return TypedResults.Ok(new StartCiRunResponse(run.Id));
    }

    public static async Task<Results<NoContent, NotFound>> FinishRun(
        Guid id,
        FinishCiRunRequest request,
        TaskFlowDbContext db,
        NotificationHub hub,
        DevOpsReportingClient devOpsReporting,
        CancellationToken ct) {
        var run = await db.CiRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        var now = DateTimeOffset.UtcNow;
        run.Status = NormalizeFinalStatus(request.Status);
        run.FinishedAt = now;
        run.DurationMs = request.DurationMs > 0 ? request.DurationMs : (int)Math.Clamp((now - run.StartedAt).TotalMilliseconds, 0, int.MaxValue);
        run.TotalProjects = request.TotalProjects;
        run.PassedProjects = request.PassedProjects;
        run.FailedProjects = request.FailedProjects;
        run.TotalTests = request.TotalTests;
        run.PassedTests = request.PassedTests;
        run.FailedTests = request.FailedTests;
        run.SkippedTests = request.SkippedTests;
        run.ErrorMessage = request.ErrorMessage;
        await db.SaveChangesAsync(ct);

        var sha = run.CommitSha.Length >= 10 ? run.CommitSha[..10] : run.CommitSha;
        var label = run.Status == "passed" ? "passed" : $"{run.Status} (failed={run.FailedTests})";
        await BroadcastAsync(hub, "ci-run-finished", $"CI run {sha} {label} — {run.ServicesInScope}", ct);

        // 2026-05-19: Bridge to devops-reporting-api so Pipeline Health'in "Unit test runs"
        // tablosu/KPI'sı dolsun. Workflow her test projesi için bir CiTestProjectResult
        // bildirmiş oluyor; bunları UnitTestCase olarak iletiyoruz. Failure detayı varsa
        // (FailureDetailsJson — `[{testName, message}, ...]`) ilk fail message proje
        // satırına embed ediliyor. Mirror best-effort — log'lanır, FinishRun'ı bloklamaz.
        var projects = await db.CiTestProjectResults
            .AsNoTracking()
            .Where(p => p.RunId == run.Id)
            .OrderBy(p => p.ProjectName)
            .ToListAsync(ct);
        var pipelineLabel = string.IsNullOrWhiteSpace(run.ServicesInScope)
            ? $"ci:{run.Branch}"
            : $"ci:{run.ServicesInScope}";
        await devOpsReporting.PublishUnitTestRunAsync(new UnitTestRunReport(
            RunId: run.Id,
            Pipeline: pipelineLabel,
            Commit: sha,
            Status: NormalizeUnitTestStatus(run.Status),
            DurationSeconds: Math.Round(run.DurationMs / 1000.0, 2),
            CompletedAt: run.FinishedAt ?? now,
            TestCases: projects.Select(p => new UnitTestRunReportCase(
                Name: string.IsNullOrWhiteSpace(p.ServiceName) ? p.ProjectName : $"{p.ServiceName} / {p.ProjectName}",
                Status: NormalizeUnitTestStatus(p.Status),
                DurationSeconds: Math.Round(p.DurationMs / 1000.0, 2),
                FailureMessage: ExtractFirstFailureMessage(p.FailureDetailsJson)
            )).ToArray()
        ), ct);

        return TypedResults.NoContent();
    }

    public static async Task<Results<Ok<CiTestProjectResultDto>, NotFound>> ReportProject(
        Guid id,
        ReportCiProjectRequest request,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var run = await db.CiRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        var now = DateTimeOffset.UtcNow;
        // Workflow her proje için tek bildirim yapıyor — aynı projeyi iki kere bildirirse
        // mevcut row update edilir. Bu sayede retry idempotent kalır.
        var existing = await db.CiTestProjectResults
            .FirstOrDefaultAsync(p => p.RunId == id && p.ProjectName == request.ProjectName, ct);
        if (existing is null) {
            existing = new CiTestProjectResult {
                RunId = id,
                ProjectName = request.ProjectName.Trim(),
                StartedAt = now
            };
            db.CiTestProjectResults.Add(existing);
        }
        existing.ServiceName = (request.ServiceName ?? string.Empty).Trim().ToLowerInvariant();
        existing.Status = NormalizeProjectStatus(request.Status);
        existing.Passed = request.Passed;
        existing.Failed = request.Failed;
        existing.Skipped = request.Skipped;
        existing.DurationMs = request.DurationMs;
        existing.FailureDetailsJson = request.Failures is { } failures
            ? System.Text.Json.JsonSerializer.Serialize(failures.Take(20).ToArray())
            : null;
        existing.FinishedAt = existing.Status is "passed" or "failed" or "error" or "skipped" ? now : null;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToProjectDto(existing));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<CiRunDto[]>> GetRuns(int? take, TaskFlowDbContext db, CancellationToken ct) {
        var limit = Math.Clamp(take ?? 50, 1, 200);
        var rows = await db.CiRuns
            .AsNoTracking()
            .OrderByDescending(r => r.StartedAt)
            .Take(limit)
            .ToListAsync(ct);
        return TypedResults.Ok(rows.Select(ToRunDto).ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<CiRunDetailDto>, NotFound>> GetRunDetail(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var run = await db.CiRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        var projects = await db.CiTestProjectResults
            .AsNoTracking()
            .Where(p => p.RunId == id)
            .OrderBy(p => p.ProjectName)
            .ToListAsync(ct);
        return TypedResults.Ok(new CiRunDetailDto(ToRunDto(run), projects.Select(ToProjectDto).ToArray()));
    }

    /// <summary>
    /// Deploy gate — auto-deploy worker tek servisin son CI sonucunu sorar. Verilen `service`
    /// için en son tamamlanmış `CiTestProjectResult.ServiceName == service` row'unu (StartedAt
    /// DESC) döner. `LastStatus = null` → CI hiç koşmadı → deploy'a izin (legacy davranış).
    /// `failed` → deploy SKIP.
    /// </summary>
    public static async Task<Ok<ServiceCiStatusDto>> GetServiceStatus(string service, TaskFlowDbContext db, CancellationToken ct) {
        var key = (service ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(key)) {
            return TypedResults.Ok(new ServiceCiStatusDto(string.Empty, null, null, null, null, 0, 0, null));
        }
        var last = await (
            from p in db.CiTestProjectResults.AsNoTracking()
            where p.ServiceName == key
                && p.Status != "running"
            join r in db.CiRuns.AsNoTracking() on p.RunId equals r.Id
            orderby p.StartedAt descending
            select new { p, r }
        ).FirstOrDefaultAsync(ct);
        if (last is null) {
            return TypedResults.Ok(new ServiceCiStatusDto(key, null, null, null, null, 0, 0, null));
        }
        return TypedResults.Ok(new ServiceCiStatusDto(
            key,
            last.p.Status,
            last.r.CommitSha,
            last.r.Branch,
            last.p.StartedAt,
            last.p.Failed,
            last.p.Passed,
            last.r.WorkflowRunUrl));
    }

    public static async Task<NoContent> NotifyDeployBlocked(
        DeployBlockedNotifyRequest request,
        NotificationHub hub,
        CancellationToken ct) {
        var env = NormalizeEnv(request.Environment);
        var services = (request.BlockedServices ?? Array.Empty<DeployBlockedServiceDto>())
            .Where(s => !string.IsNullOrWhiteSpace(s.ServiceName))
            .Take(20)
            .ToArray();
        if (services.Length == 0) {
            return TypedResults.NoContent();
        }
        var summary = string.Join(", ", services.Select(s => $"{s.ServiceName} (failed={s.FailedTests})"));
        var msg = $"Auto-deploy {env}: CI gate {services.Length} servis için deploy'ı bloke etti — {summary}. Testler düzelene kadar bu servisler atlanacak.";
        await BroadcastAsync(hub, "ci-deploy-blocked", msg, ct);
        return TypedResults.NoContent();
    }

    // ----------------- Helpers -----------------

    private static string? SerializeList(IEnumerable<string>? items) {
        if (items is null) return null;
        var arr = items.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
        if (arr.Length == 0) return null;
        return System.Text.Json.JsonSerializer.Serialize(arr);
    }

    private static string? TruncateMessage(string? msg) {
        if (string.IsNullOrWhiteSpace(msg)) return null;
        var firstLine = msg.Split('\n', 2)[0].Trim();
        return firstLine.Length > 1000 ? firstLine[..1000] : firstLine;
    }

    private static string NormalizeEnv(string? value) {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;
        var lower = value.Trim().ToLowerInvariant();
        return lower switch {
            "development" => "dev",
            "stg" => "staging",
            "production" => "prod",
            _ => lower
        };
    }

    private static string NormalizeFinalStatus(string status) {
        var lower = (status ?? "").Trim().ToLowerInvariant();
        return lower switch {
            "passed" or "success" or "succeeded" => "passed",
            "failed" or "failure" => "failed",
            "error" or "errored" or "crashed" => "error",
            "cancelled" or "canceled" => "cancelled",
            _ => "failed"
        };
    }

    private static string NormalizeProjectStatus(string status) {
        var lower = (status ?? "").Trim().ToLowerInvariant();
        return lower switch {
            "passed" or "success" => "passed",
            "failed" or "failure" => "failed",
            "error" or "crashed" => "error",
            "skipped" or "skip" => "skipped",
            "running" => "running",
            _ => "running"
        };
    }

    // devops-reporting-api'nin UnitTestRuns tablosu sadece "passed"/"failed" gibi
    // sınırlı bir set tanıyor; CI tarafındaki "error"/"cancelled" değerlerini
    // failed altında topluyoruz ki KPI failure-rate doğru çıksın.
    private static string NormalizeUnitTestStatus(string status) {
        var lower = (status ?? "").Trim().ToLowerInvariant();
        return lower switch {
            "passed" or "success" => "passed",
            "skipped" => "skipped",
            _ => "failed"
        };
    }

    // FailureDetailsJson formatı: [{ "testName": "...", "message": "..." }, ...]
    // İlk failure'ın mesajını proje satırının FailureMessage'ına embed ediyoruz.
    private static string? ExtractFirstFailureMessage(string? json) {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try {
            using var doc = System.Text.Json.JsonDocument.Parse(json);
            if (doc.RootElement.ValueKind != System.Text.Json.JsonValueKind.Array) return null;
            foreach (var item in doc.RootElement.EnumerateArray()) {
                var name = item.TryGetProperty("testName", out var n) ? n.GetString() : null;
                var msg = item.TryGetProperty("message", out var m) ? m.GetString() : null;
                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(msg)) continue;
                var combined = string.IsNullOrWhiteSpace(name) ? msg : $"{name}: {msg}";
                return combined?.Length > 1000 ? combined[..1000] : combined;
            }
        }
        catch {
            // best-effort; ill-formed json → no embed.
        }
        return null;
    }

    private static CiRunDto ToRunDto(CiRun r) {
        var services = string.IsNullOrEmpty(r.ServicesInScope)
            ? Array.Empty<string>()
            : r.ServicesInScope.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        return new CiRunDto(
            r.Id, r.CommitSha, r.Branch, r.Author, r.AuthorEmail, r.CommitMessage,
            services, r.Status, r.StartedAt, r.FinishedAt, r.DurationMs,
            r.TotalProjects, r.PassedProjects, r.FailedProjects,
            r.TotalTests, r.PassedTests, r.FailedTests, r.SkippedTests,
            r.WorkflowRunUrl, r.RunnerName, r.EnvironmentScope, r.ErrorMessage);
    }

    private static CiTestProjectResultDto ToProjectDto(CiTestProjectResult p) => new(
        p.Id, p.RunId, p.ProjectName, p.ServiceName, p.Status,
        p.Passed, p.Failed, p.Skipped, p.DurationMs,
        p.FailureDetailsJson, p.StartedAt, p.FinishedAt);

    private static async Task BroadcastAsync(NotificationHub hub, string kind, string message, CancellationToken ct) {
        try {
            await hub.BroadcastAsync(new WorkItemNotificationDto(
                Ulid.NewUlid().ToGuid(),
                Guid.Empty,
                kind,
                kind == "ci-deploy-blocked" ? "warning" : "info",
                "CI",
                message,
                "in-app",
                DateTimeOffset.UtcNow,
                null), ct);
        }
        catch {
            // best-effort; UI'a hub kapalıysa CI run yine de yazılır.
        }
    }
}
