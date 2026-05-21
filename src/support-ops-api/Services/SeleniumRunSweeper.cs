using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Models;

namespace SupportOpsApi.Services;

/// <summary>
/// Reclaims Selenium runs left in flight after an unclean shutdown.
/// Aspire/host restarts kill running test processes without flushing a terminal
/// status, so runs stay at <c>claimed</c>/<c>running</c> forever and block the
/// queue UI. This sweeper promotes those rows to <c>timeout</c> with a clear
/// failure message so the dashboard no longer shows ghost activity.
/// </summary>
public static class SeleniumRunSweeper {
    private const string OrphanMessage = "Runner restart — orphaned run reclaimed on startup.";

    /// <summary>
    /// Called once on support-ops-api startup. Sweeps every run still in
    /// <c>claimed</c>/<c>running</c> — after an Aspire restart no runner can
    /// possibly still be executing them, so it is safe to terminate them all.
    /// </summary>
    public static async Task<int> SweepOnStartupAsync(TaskFlowDbContext db, CancellationToken ct) {
        var stuck = await db.SeleniumRuns
            .Where(r => r.Status == "claimed" || r.Status == "running")
            .ToListAsync(ct);
        if (stuck.Count == 0) {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var run in stuck) {
            TerminateAsTimeout(run, now);
        }
        await db.SaveChangesAsync(ct);
        return stuck.Count;
    }

    /// <summary>
    /// Called when a runner registers. A runner process restart means any run it
    /// previously held is unrecoverable — mark those as timeout so the queue can
    /// drain. Unlike the startup sweep this is scoped to a single runner so
    /// other active runners are unaffected.
    /// </summary>
    public static async Task<int> SweepForRunnerAsync(TaskFlowDbContext db, Guid runnerId, CancellationToken ct) {
        var stuck = await db.SeleniumRuns
            .Where(r => r.RunnerId == runnerId && (r.Status == "claimed" || r.Status == "running"))
            .ToListAsync(ct);
        if (stuck.Count == 0) {
            return 0;
        }

        var now = DateTimeOffset.UtcNow;
        foreach (var run in stuck) {
            TerminateAsTimeout(run, now);
        }
        await db.SaveChangesAsync(ct);
        return stuck.Count;
    }

    private static void TerminateAsTimeout(SeleniumRun run, DateTimeOffset now) {
        run.Status = "timeout";
        run.FinishedAt = now;
        run.FailureMessage = string.IsNullOrWhiteSpace(run.FailureMessage)
            ? OrphanMessage
            : run.FailureMessage + " | " + OrphanMessage;
        if (run.StartedAt is { } started) {
            run.DurationMs = Math.Max(run.DurationMs, (int)(now - started).TotalMilliseconds);
        }
    }
}
