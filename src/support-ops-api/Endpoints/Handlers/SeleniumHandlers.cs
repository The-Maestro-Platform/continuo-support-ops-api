using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints.Contracts;
using SupportOpsApi.Models;
using SupportOpsApi.Services;
using Continuo.Observability.Attributes;

namespace SupportOpsApi.Endpoints.Handlers;

/// <summary>
/// Handles the Selenium runner pipeline: catalog CRUD, flow CRUD, run queue,
/// and the runner-facing register/claim/status protocol. Runs that end in
/// <c>failed</c> automatically create an auto-bug work item using the same helpers
/// as <see cref="WorkItemHandlers.CreateAutoBug"/>.
/// </summary>
public static class SeleniumHandlers {
    // ----------------- Catalog -----------------

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<SeleniumTestDto[]>> GetTests(TaskFlowDbContext db, bool? includeInactive, CancellationToken ct) {
        var query = db.SeleniumTests.AsNoTracking().AsQueryable();
        if (includeInactive != true) {
            query = query.Where(t => t.IsActive);
        }
        var items = await query.OrderBy(t => t.Name).ToListAsync(ct);
        return TypedResults.Ok(items.Select(ToTestDto).ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumTestDto>, NotFound>> GetTestById(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var t = await db.SeleniumTests.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        return t is null ? TypedResults.NotFound() : TypedResults.Ok(ToTestDto(t));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Created<SeleniumTestDto>, BadRequest<string>>> CreateTest(
        UpsertSeleniumTestRequest request,
        TaskFlowDbContext db,
        HttpContext httpContext,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) {
            return TypedResults.BadRequest("Code and Name are required");
        }
        if (!IsValidKind(request.Kind)) {
            return TypedResults.BadRequest("Kind must be 'code' or 'scenario'");
        }
        if (await db.SeleniumTests.AnyAsync(x => x.Code == request.Code, ct)) {
            return TypedResults.BadRequest($"A test with code '{request.Code}' already exists");
        }

        var now = DateTimeOffset.UtcNow;
        var actor = ResolveActor(httpContext);
        var entity = new SeleniumTest {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            Kind = request.Kind.Trim().ToLowerInvariant(),
            CodeFullyQualifiedName = request.CodeFullyQualifiedName,
            ScenarioJson = request.ScenarioJson,
            Tags = JoinTags(request.Tags),
            TimeoutSeconds = request.TimeoutSeconds is > 0 ? request.TimeoutSeconds.Value : 600,
            IsActive = request.IsActive ?? true,
            CreatedBy = actor,
            CreatedAt = now,
            UpdatedAt = now
        };
        db.SeleniumTests.Add(entity);
        await db.SaveChangesAsync(ct);
        return TypedResults.Created($"/selenium/tests/{entity.Id}", ToTestDto(entity));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumTestDto>, NotFound, BadRequest<string>>> UpdateTest(
        Guid id,
        UpsertSeleniumTestRequest request,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var entity = await db.SeleniumTests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }
        if (!IsValidKind(request.Kind)) {
            return TypedResults.BadRequest("Kind must be 'code' or 'scenario'");
        }
        entity.Code = request.Code.Trim();
        entity.Name = request.Name.Trim();
        entity.Description = request.Description;
        entity.Kind = request.Kind.Trim().ToLowerInvariant();
        entity.CodeFullyQualifiedName = request.CodeFullyQualifiedName;
        entity.ScenarioJson = request.ScenarioJson;
        entity.Tags = JoinTags(request.Tags);
        entity.TimeoutSeconds = request.TimeoutSeconds is > 0 ? request.TimeoutSeconds.Value : entity.TimeoutSeconds;
        entity.IsActive = request.IsActive ?? entity.IsActive;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(ToTestDto(entity));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> DeleteTest(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var entity = await db.SeleniumTests.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (entity is null) {
            return TypedResults.NotFound();
        }
        // Soft delete to preserve historical runs
        entity.IsActive = false;
        entity.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // ----------------- Flows -----------------

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<SeleniumFlowDto[]>> GetFlows(TaskFlowDbContext db, CancellationToken ct) {
        var items = await db.SeleniumFlows
            .AsNoTracking()
            .Include(f => f.Steps.OrderBy(s => s.Order))
                .ThenInclude(s => s.Test)
            .OrderBy(f => f.Name)
            .ToListAsync(ct);
        return TypedResults.Ok(items.Select(ToFlowDto).ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Created<SeleniumFlowDto>, BadRequest<string>>> CreateFlow(
        UpsertSeleniumFlowRequest request,
        TaskFlowDbContext db,
        HttpContext httpContext,
        CancellationToken ct) {
        if (string.IsNullOrWhiteSpace(request.Code) || string.IsNullOrWhiteSpace(request.Name)) {
            return TypedResults.BadRequest("Code and Name are required");
        }
        if (await db.SeleniumFlows.AnyAsync(x => x.Code == request.Code, ct)) {
            return TypedResults.BadRequest($"A flow with code '{request.Code}' already exists");
        }

        var now = DateTimeOffset.UtcNow;
        var flow = new SeleniumFlow {
            Code = request.Code.Trim(),
            Name = request.Name.Trim(),
            Description = request.Description,
            IsActive = request.IsActive ?? true,
            CreatedBy = ResolveActor(httpContext),
            CreatedAt = now,
            UpdatedAt = now
        };
        var order = 1;
        foreach (var step in (request.Steps ?? Enumerable.Empty<UpsertSeleniumFlowStepRequest>()).OrderBy(s => s.Order)) {
            flow.Steps.Add(new SeleniumFlowStep {
                FlowId = flow.Id,
                Order = step.Order > 0 ? step.Order : order,
                TestId = step.TestId,
                StopOnFailure = step.StopOnFailure
            });
            order++;
        }
        db.SeleniumFlows.Add(flow);
        await db.SaveChangesAsync(ct);

        var reloaded = await db.SeleniumFlows
            .Include(f => f.Steps.OrderBy(s => s.Order))
                .ThenInclude(s => s.Test)
            .FirstAsync(f => f.Id == flow.Id, ct);
        return TypedResults.Created($"/selenium/flows/{flow.Id}", ToFlowDto(reloaded));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumFlowDto>, NotFound>> UpdateFlow(
        Guid id,
        UpsertSeleniumFlowRequest request,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var flow = await db.SeleniumFlows
            .Include(f => f.Steps)
            .FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flow is null) {
            return TypedResults.NotFound();
        }
        flow.Code = request.Code.Trim();
        flow.Name = request.Name.Trim();
        flow.Description = request.Description;
        flow.IsActive = request.IsActive ?? flow.IsActive;
        flow.UpdatedAt = DateTimeOffset.UtcNow;

        db.SeleniumFlowSteps.RemoveRange(flow.Steps);
        flow.Steps.Clear();
        var order = 1;
        foreach (var step in (request.Steps ?? Enumerable.Empty<UpsertSeleniumFlowStepRequest>()).OrderBy(s => s.Order)) {
            flow.Steps.Add(new SeleniumFlowStep {
                FlowId = flow.Id,
                Order = step.Order > 0 ? step.Order : order,
                TestId = step.TestId,
                StopOnFailure = step.StopOnFailure
            });
            order++;
        }
        await db.SaveChangesAsync(ct);

        var reloaded = await db.SeleniumFlows
            .Include(f => f.Steps.OrderBy(s => s.Order))
                .ThenInclude(s => s.Test)
            .FirstAsync(f => f.Id == flow.Id, ct);
        return TypedResults.Ok(ToFlowDto(reloaded));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> DeleteFlow(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var flow = await db.SeleniumFlows.FirstOrDefaultAsync(f => f.Id == id, ct);
        if (flow is null) {
            return TypedResults.NotFound();
        }
        flow.IsActive = false;
        flow.UpdatedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    // ----------------- Runs queue (UI-facing) -----------------

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<SeleniumRunDto[]>> GetRuns(TaskFlowDbContext db, string? status, int? take, CancellationToken ct) {
        var query = db.SeleniumRuns
            .AsNoTracking()
            .Include(r => r.Test)
            .Include(r => r.Flow)
            .Include(r => r.Runner)
            .AsQueryable();
        if (!string.IsNullOrWhiteSpace(status)) {
            query = query.Where(r => r.Status == status);
        }
        var runs = await query
            .OrderByDescending(r => r.QueuedAt)
            .Take(take is > 0 and <= 500 ? take.Value : 200)
            .ToListAsync(ct);
        return TypedResults.Ok(runs.Select(ToRunDto).ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumRunStatusDto>, NotFound>> GetRunStatus(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var row = await db.SeleniumRuns
            .Where(r => r.Id == id)
            .Select(r => new { r.Id, r.Status })
            .FirstOrDefaultAsync(ct);
        if (row is null) {
            return TypedResults.NotFound();
        }
        return TypedResults.Ok(new SeleniumRunStatusDto(row.Id, row.Status));
    }

    public static async Task<Results<Ok<SeleniumRunLogDto>, NotFound>> GetRunLog(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var run = await db.SeleniumRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null
            ? TypedResults.NotFound()
            : TypedResults.Ok(new SeleniumRunLogDto(run.Id, run.Stdout, run.Stderr));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumRunStepDto[]>, NotFound>> GetRunSteps(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var run = await db.SeleniumRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        var steps = await db.SeleniumRunSteps
            .AsNoTracking()
            .Where(s => s.RunId == id)
            .OrderBy(s => s.Order)
            .ToListAsync(ct);
        return TypedResults.Ok(steps.Select(ToStepDto).ToArray());
    }

    public static async Task<Results<Ok<SeleniumRunStepDto[]>, NotFound, BadRequest<string>>> ReportRunSteps(
        Guid id,
        RunStepPayload[] steps,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var run = await db.SeleniumRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        if (steps.Length == 0) {
            return TypedResults.BadRequest("At least one step is required");
        }

        var now = DateTimeOffset.UtcNow;
        var entities = UpsertSteps(db, run, steps, now);
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(entities.Select(ToStepDto).ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Created<SeleniumRunDto[]>, BadRequest<string>>> QueueRun(
        QueueRunRequest request,
        TaskFlowDbContext db,
        NotificationHub hub,
        HttpContext httpContext,
        CancellationToken ct) {
        var actor = ResolveActor(httpContext);
        var now = DateTimeOffset.UtcNow;
        var priority = request.Priority is > 0 and <= 5 ? request.Priority.Value : 3;
        // Env-scoped enqueue: UI hostname'inden çıkardığı `dev` / `staging` / `prod` değerini
        // EnvironmentScope ile gönderir. Sunucu tarafı X-TC-Env header'ını fallback olarak da kabul eder
        // (UI bunu set ediyorsa). Boş kalırsa run tüm runner'lara açık (legacy davranış).
        var envScope = NormalizeEnvironment(request.EnvironmentScope);
        if (string.IsNullOrEmpty(envScope) && httpContext.Request.Headers.TryGetValue("X-TC-Env", out var headerEnv)) {
            envScope = NormalizeEnvironment(headerEnv.ToString());
        }
        var created = new List<SeleniumRun>();

        if (request.FlowId is Guid flowId) {
            var flow = await db.SeleniumFlows
                .Include(f => f.Steps.OrderBy(s => s.Order))
                    .ThenInclude(s => s.Test)
                .FirstOrDefaultAsync(f => f.Id == flowId, ct);
            if (flow is null) {
                return TypedResults.BadRequest($"Flow '{flowId}' not found");
            }
            if (flow.Steps.Count == 0) {
                return TypedResults.BadRequest("Flow has no steps");
            }
            var batchId = Ulid.NewUlid().ToGuid();
            var envJson = request.Environment is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Environment) : null;
            foreach (var step in flow.Steps.OrderBy(s => s.Order)) {
                created.Add(new SeleniumRun {
                    TestId = step.TestId,
                    FlowId = flow.Id,
                    FlowStepOrder = step.Order,
                    FlowBatchId = batchId,
                    Status = "queued",
                    Priority = priority,
                    QueuedBy = actor,
                    QueuedAt = now,
                    EnvironmentJson = envJson,
                    Environment = string.IsNullOrEmpty(envScope) ? null : envScope,
                    TargetRunnerId = request.TargetRunnerId
                });
            }
        } else if (request.TestId is Guid testId) {
            if (!await db.SeleniumTests.AnyAsync(t => t.Id == testId, ct)) {
                return TypedResults.BadRequest($"Test '{testId}' not found");
            }
            var envJson2 = request.Environment is { Count: > 0 } ? System.Text.Json.JsonSerializer.Serialize(request.Environment) : null;
            created.Add(new SeleniumRun {
                TestId = testId,
                Status = "queued",
                Priority = priority,
                QueuedBy = actor,
                QueuedAt = now,
                EnvironmentJson = envJson2,
                Environment = string.IsNullOrEmpty(envScope) ? null : envScope,
                TargetRunnerId = request.TargetRunnerId
            });
        } else {
            return TypedResults.BadRequest("TestId or FlowId is required");
        }

        db.SeleniumRuns.AddRange(created);
        await db.SaveChangesAsync(ct);

        var dtos = new List<SeleniumRunDto>();
        foreach (var r in created) {
            var reloaded = await db.SeleniumRuns
                .AsNoTracking()
                .Include(x => x.Test)
                .Include(x => x.Flow)
                .FirstAsync(x => x.Id == r.Id, ct);
            dtos.Add(ToRunDto(reloaded));
        }

        await BroadcastRunEventAsync(hub, "selenium-run-queued", $"{dtos.Count} run(s) queued", ct);
        return TypedResults.Created("/selenium/runs", dtos.ToArray());
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, NotFound>> CancelRun(Guid id, TaskFlowDbContext db, NotificationHub hub, CancellationToken ct) {
        var run = await db.SeleniumRuns.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        // `running` runs are also cancellable — runners poll the run status and kill the in-flight
        // `dotnet test` process when they see the status flip to `cancelled`. Terminal states
        // (`passed`/`failed`/`timeout`/`cancelled`) are no-ops.
        if (run.Status is "queued" or "claimed" or "running") {
            run.Status = "cancelled";
            run.FinishedAt = DateTimeOffset.UtcNow;
            // Release the runner's single-slot tracking so the runner list doesn't show the run forever.
            if (run.RunnerId is Guid runnerId) {
                var runner = await db.SeleniumRunners.FirstOrDefaultAsync(x => x.Id == runnerId, ct);
                if (runner is not null && runner.CurrentRunId == run.Id) {
                    runner.CurrentRunId = null;
                    runner.Status = "idle";
                }
            }
            await db.SaveChangesAsync(ct);
            await BroadcastRunEventAsync(hub, "selenium-run-cancelled", $"Run {run.Id} cancelled", ct);
        }
        return TypedResults.NoContent();
    }

    // ----------------- Runner protocol -----------------

    public static async Task<Ok<RegisterRunnerResponse>> RegisterRunner(
        RegisterRunnerRequest request,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var now = DateTimeOffset.UtcNow;
        var existing = await db.SeleniumRunners.FirstOrDefaultAsync(r => r.Name == request.Name, ct);
        var env = NormalizeEnvironment(request.Environment);
        if (existing is null) {
            existing = new SeleniumRunner {
                Name = request.Name.Trim(),
                Location = string.IsNullOrWhiteSpace(request.Location) ? "local" : request.Location.Trim().ToLowerInvariant(),
                Environment = env,
                MachineName = request.MachineName ?? string.Empty,
                Version = request.Version ?? string.Empty,
                Status = "idle",
                Capabilities = JoinTags(request.Capabilities),
                MaxParallel = ClampMaxParallel(request.MaxParallel),
                LastHeartbeatAt = now,
                CreatedAt = now
            };
            db.SeleniumRunners.Add(existing);
        } else {
            existing.Location = string.IsNullOrWhiteSpace(request.Location) ? existing.Location : request.Location.Trim().ToLowerInvariant();
            // Environment runner-side appsettings'den geliyor. Boş ise mevcut DB değerini koru
            // (operatör UI'dan elle override etmiş olabilir), aksi halde runner-reported değer wins —
            // bir container'ı dev'den staging'e taşıdığınızda otomatik takip etsin.
            if (!string.IsNullOrEmpty(env)) {
                existing.Environment = env;
            }
            existing.MachineName = request.MachineName ?? existing.MachineName;
            existing.Version = request.Version ?? existing.Version;
            existing.Capabilities = JoinTags(request.Capabilities);
            existing.Status = "idle";
            existing.LastHeartbeatAt = now;
            // Runner-reported MaxParallel is the *local* default from appsettings; we only take it if
            // the runner was never configured via UI (i.e. still at default 1). Otherwise the DB value
            // (set via UpdateRunner / UI) wins so operators can tune concurrency without redeploying.
            if (request.MaxParallel.HasValue && existing.MaxParallel <= 1) {
                existing.MaxParallel = ClampMaxParallel(request.MaxParallel);
            }
        }
        await db.SaveChangesAsync(ct);

        // Runner just (re)started — any run it was holding is unrecoverable.
        // Reclaim them so they don't wedge the queue indefinitely.
        try {
            await SeleniumRunSweeper.SweepForRunnerAsync(db, existing.Id, ct);
        }
        catch {
            // best-effort — registration itself must still succeed
        }

        return TypedResults.Ok(new RegisterRunnerResponse(existing.Id, existing.MaxParallel));
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<SeleniumRunnerDto>, NotFound, BadRequest<string>>> UpdateRunner(
        Guid id,
        UpdateRunnerRequest request,
        TaskFlowDbContext db,
        CancellationToken ct) {
        var runner = await db.SeleniumRunners.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (runner is null) {
            return TypedResults.NotFound();
        }
        if (request.MaxParallel.HasValue) {
            var requested = request.MaxParallel.Value;
            if (requested < 1 || requested > 8) {
                return TypedResults.BadRequest("MaxParallel must be between 1 and 8.");
            }
            runner.MaxParallel = requested;
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.Ok(MapRunnerDto(runner));
    }

    private static int ClampMaxParallel(int? value) {
        if (!value.HasValue) {
            return 1;
        }
        return Math.Clamp(value.Value, 1, 8);
    }

    public static async Task<Results<NoContent, NotFound>> Heartbeat(Guid id, TaskFlowDbContext db, CancellationToken ct) {
        var runner = await db.SeleniumRunners.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (runner is null) {
            return TypedResults.NotFound();
        }
        runner.LastHeartbeatAt = DateTimeOffset.UtcNow;
        if (runner.Status == "offline") {
            runner.Status = "idle";
        }
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    public static async Task<Results<Ok<ClaimRunResponse>, NotFound>> ClaimNextRun(
        Guid id,
        TaskFlowDbContext db,
        NotificationHub hub,
        CancellationToken ct) {
        var runner = await db.SeleniumRunners.FirstOrDefaultAsync(r => r.Id == id, ct);
        if (runner is null) {
            return TypedResults.NotFound();
        }

        // Naive FIFO + priority claim. Guarded by an EF transaction so two runners can't race.
        // Must run through the retrying execution strategy because SqlServer's retry policy
        // otherwise refuses user-initiated transactions.
        SeleniumRun? next = null;
        var strategy = db.Database.CreateExecutionStrategy();
        var runnerEnv = runner.Environment ?? string.Empty;
        await strategy.ExecuteAsync(async () => {
            await using var tx = await db.Database.BeginTransactionAsync(ct);
            // Env filtreleme: runner.Environment set'li ise sadece aynı ortama ait (veya `Environment`
            // belirtilmemiş eski) run'ları çek. Runner.Environment boş ise (geriye uyumlu single-tenant
            // setup) tüm run'lara aday — eski davranış aynen korunur.
            var candidate = await db.SeleniumRuns
                .Include(r => r.Test)
                .Where(r => r.Status == "queued")
                .Where(r => r.TargetRunnerId == null || r.TargetRunnerId == runner.Id)
                .Where(r => runnerEnv == string.Empty || r.Environment == null || r.Environment == runnerEnv)
                .OrderBy(r => r.Priority)
                .ThenBy(r => r.QueuedAt)
                .FirstOrDefaultAsync(ct);
            if (candidate is null || candidate.Test is null) {
                await tx.RollbackAsync(ct);
                next = null;
                return;
            }
            var now = DateTimeOffset.UtcNow;
            candidate.Status = "claimed";
            candidate.RunnerId = runner.Id;
            candidate.ClaimedAt = now;
            candidate.AttemptCount += 1;
            runner.Status = "busy";
            runner.CurrentRunId = candidate.Id;
            runner.LastHeartbeatAt = now;
            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            next = candidate;
        });

        if (next is null || next.Test is null) {
            return TypedResults.NotFound();
        }

        await BroadcastRunEventAsync(hub, "selenium-run-claimed", $"Run {next.Id} claimed by {runner.Name}", ct);

        return TypedResults.Ok(new ClaimRunResponse(
            next.Id,
            next.Test.Id,
            next.Test.Kind,
            next.Test.Code,
            next.Test.Name,
            next.Test.CodeFullyQualifiedName,
            next.Test.ScenarioJson,
            next.Test.TimeoutSeconds,
            next.EnvironmentJson));
    }

    public static async Task<Results<NoContent, NotFound, BadRequest<string>>> UpdateRunStatus(
        Guid id,
        RunStatusUpdateRequest request,
        TaskFlowDbContext db,
        NotificationHub hub,
        IConfiguration configuration,
        DevOpsReportingClient devOpsReporting,
        CancellationToken ct) {
        var run = await db.SeleniumRuns
            .Include(r => r.Test)
            .Include(r => r.Flow)
            .FirstOrDefaultAsync(r => r.Id == id, ct);
        if (run is null) {
            return TypedResults.NotFound();
        }
        if (!IsValidRunStatus(request.Status)) {
            return TypedResults.BadRequest("Invalid run status");
        }

        var now = DateTimeOffset.UtcNow;
        var previousStatus = run.Status;
        run.Status = request.Status;
        if (request.Stdout is not null) {
            run.Stdout = (run.Stdout ?? string.Empty) + request.Stdout;
        }
        if (request.Stderr is not null) {
            run.Stderr = (run.Stderr ?? string.Empty) + request.Stderr;
        }
        if (!string.IsNullOrWhiteSpace(request.FailureMessage)) {
            run.FailureMessage = request.FailureMessage;
        }
        if (!string.IsNullOrWhiteSpace(request.ScreenshotRef)) {
            run.ScreenshotRef = request.ScreenshotRef;
        }
        if (request.DurationMs is > 0) {
            run.DurationMs = request.DurationMs.Value;
        }

        if (run.Status == "running" && run.StartedAt is null) {
            run.StartedAt = now;
        }
        if (run.Status is "passed" or "failed" or "timeout" or "cancelled" or "skipped") {
            run.FinishedAt = now;
            if (run.RunnerId is Guid runnerId) {
                var runner = await db.SeleniumRunners.FirstOrDefaultAsync(r => r.Id == runnerId, ct);
                if (runner is not null) {
                    // With MaxParallel > 1 a runner can hold several in-flight runs. Only flip to
                    // idle once *all* of its non-terminal runs are done; otherwise it's still busy.
                    var stillActive = await db.SeleniumRuns
                        .AnyAsync(r => r.RunnerId == runnerId
                                       && r.Id != run.Id
                                       && (r.Status == "claimed" || r.Status == "running"), ct);
                    if (!stillActive) {
                        runner.Status = "idle";
                        runner.CurrentRunId = null;
                    } else if (runner.CurrentRunId == run.Id) {
                        // Don't leave CurrentRunId pointing at a finished run.
                        runner.CurrentRunId = null;
                    }
                    runner.LastHeartbeatAt = now;
                }
            }
        }

        // If a flow-step run fails with stop-on-failure, cancel remaining runs in the batch.
        if (run.Status == "failed" && run.FlowBatchId is Guid batchId && run.FlowId is Guid flowId) {
            var step = await db.SeleniumFlowSteps
                .FirstOrDefaultAsync(s => s.FlowId == flowId && s.Order == run.FlowStepOrder, ct);
            if (step is not null && step.StopOnFailure) {
                var remaining = await db.SeleniumRuns
                    .Where(r => r.FlowBatchId == batchId && r.Status == "queued")
                    .ToListAsync(ct);
                foreach (var r in remaining) {
                    r.Status = "cancelled";
                    r.FinishedAt = now;
                }
            }
        }

        if (request.Steps is not null) {
            var stepList = request.Steps.ToArray();
            if (stepList.Length > 0) {
                UpsertSteps(db, run, stepList, now);
            }
        }

        await db.SaveChangesAsync(ct);

        // If failed, fire the auto-bug flow
        if (run.Status == "failed" && run.AutoBugWorkItemId is null) {
            try {
                var workItemId = await SeleniumAutoBugBridge.CreateAutoBugAsync(db, hub, configuration, run, ct);
                run.AutoBugWorkItemId = workItemId;
                await db.SaveChangesAsync(ct);
            }
            catch {
                // best-effort — the run result is already persisted
            }
        }

        // If passed (or skipped without an error), auto-resolve any open bug previously created
        // by a failure of the same test — a skip means the test isn't a regression right now
        // (environment not reachable, API timeout, etc.), so stale bugs shouldn't linger.
        if ((run.Status == "passed" || run.Status == "skipped") && previousStatus != run.Status) {
            try {
                await SeleniumAutoBugBridge.AutoResolveAsync(db, hub, run, ct);
            }
            catch {
                // best-effort
            }
        }

        if (previousStatus != run.Status) {
            await BroadcastRunEventAsync(hub, $"selenium-run-{run.Status}", $"Run {run.Id} → {run.Status}", ct);
        }

        // Bridge terminal results to devops-reporting-api so Pipeline Health reflects this run.
        // Best-effort: PublishSeleniumRunAsync swallows transport failures internally.
        if (previousStatus != run.Status &&
            run.Status is "passed" or "failed" or "timeout" or "cancelled" or "skipped") {
            var startedAt = run.StartedAt ?? run.QueuedAt;
            var durationSeconds = run.DurationMs > 0
                ? run.DurationMs / 1000.0
                : (run.FinishedAt is { } finished ? Math.Max(0, (finished - startedAt).TotalSeconds) : 0);
            var stepReports = run.Steps?
                .OrderBy(s => s.Order)
                .Select(s => new SeleniumRunReportStep(
                    Title: s.Title,
                    Status: s.Status,
                    ScreenshotUrl: s.ScreenshotRef,
                    ConsoleLog: s.ConsoleLog))
                .ToList();
            var report = new SeleniumRunReport(
                RunId: run.Id,
                Suite: run.Test?.Name ?? run.Test?.Code ?? "selenium",
                Branch: configuration["DEVOPS_REPORTING__BRANCH"] ?? "main",
                BuildId: run.Id.ToString("N"),
                Environment: configuration["DEVOPS_REPORTING__ENVIRONMENT"] ?? "dev",
                Status: NormalizeStatus(run.Status),
                DurationSeconds: Math.Round(durationSeconds, 2),
                StartedAt: startedAt,
                Steps: stepReports);
            await devOpsReporting.PublishSeleniumRunAsync(report, ct);
        }
        return TypedResults.NoContent();
    }

    private static string NormalizeStatus(string status) => status switch {
        "timeout" => "failed",
        "cancelled" => "failed",
        _ => status
    };

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<SeleniumRunnerDto[]>> GetRunners(TaskFlowDbContext db, CancellationToken ct) {
        var cutoff = DateTimeOffset.UtcNow.AddMinutes(-2);
        var runners = await db.SeleniumRunners.AsNoTracking().OrderBy(r => r.Name).ToListAsync(ct);
        // Mark stale runners as offline
        foreach (var r in runners) {
            if (r.LastHeartbeatAt < cutoff && r.Status != "offline") {
                r.Status = "offline";
            }
        }
        return TypedResults.Ok(runners.Select(ToRunnerDto).ToArray());
    }

    // ----------------- Helpers -----------------

    private static string JoinTags(IEnumerable<string>? tags) =>
        string.Join(",", (tags ?? Array.Empty<string>())
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .Select(t => t.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase));

    private static string[] SplitTags(string csv) =>
        string.IsNullOrWhiteSpace(csv)
            ? Array.Empty<string>()
            : csv.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    private static bool IsValidKind(string? kind) =>
        kind is not null && (kind.Equals("code", StringComparison.OrdinalIgnoreCase) || kind.Equals("scenario", StringComparison.OrdinalIgnoreCase));

    private static bool IsValidRunStatus(string status) =>
        status is "queued" or "claimed" or "running" or "passed" or "failed" or "cancelled" or "timeout" or "skipped";

    private static SeleniumTestDto ToTestDto(SeleniumTest t) => new(
        t.Id, t.Code, t.Name, t.Description, t.Kind, t.CodeFullyQualifiedName, t.ScenarioJson,
        SplitTags(t.Tags), t.TimeoutSeconds, t.IsActive, t.CreatedBy, t.CreatedAt, t.UpdatedAt);

    private static SeleniumFlowDto ToFlowDto(SeleniumFlow f) => new(
        f.Id, f.Code, f.Name, f.Description, f.IsActive, f.CreatedBy, f.CreatedAt, f.UpdatedAt,
        f.Steps.OrderBy(s => s.Order).Select(s => new SeleniumFlowStepDto(
            s.Id, s.Order, s.TestId, s.Test?.Code, s.Test?.Name, s.StopOnFailure)).ToArray());

    private static SeleniumRunDto ToRunDto(SeleniumRun r) => new(
        r.Id, r.TestId, r.Test?.Code, r.Test?.Name,
        r.FlowId, r.Flow?.Code, r.FlowStepOrder, r.FlowBatchId,
        r.RunnerId, r.Runner?.Name,
        r.Status, r.Priority, r.AttemptCount, r.QueuedBy,
        r.QueuedAt, r.ClaimedAt, r.StartedAt, r.FinishedAt, r.DurationMs,
        r.FailureMessage, r.ScreenshotRef, r.AutoBugWorkItemId);

    private static SeleniumRunStepDto ToStepDto(SeleniumRunStep s) => new(
        s.Id, s.RunId, s.Order, s.Title, s.Status, s.DurationMs,
        s.ErrorMessage, s.ScreenshotRef, s.ConsoleLog, s.StartedAt, s.FinishedAt);

    private static List<SeleniumRunStep> UpsertSteps(TaskFlowDbContext db, SeleniumRun run, RunStepPayload[] steps, DateTimeOffset now) {
        var existing = db.SeleniumRunSteps.Where(s => s.RunId == run.Id).ToList();
        var result = new List<SeleniumRunStep>();

        foreach (var payload in steps) {
            var entity = existing.FirstOrDefault(e => e.Order == payload.Order);
            if (entity is null) {
                entity = new SeleniumRunStep {
                    RunId = run.Id,
                    Order = payload.Order,
                    Title = payload.Title.Trim(),
                    Status = payload.Status,
                    DurationMs = payload.DurationMs ?? 0,
                    ErrorMessage = payload.ErrorMessage,
                    ScreenshotRef = payload.ScreenshotRef,
                    ConsoleLog = payload.ConsoleLog,
                    StartedAt = now,
                    FinishedAt = payload.Status is "passed" or "failed" or "skipped" ? now : null
                };
                db.SeleniumRunSteps.Add(entity);
            } else {
                entity.Title = payload.Title.Trim();
                entity.Status = payload.Status;
                entity.DurationMs = payload.DurationMs ?? entity.DurationMs;
                if (!string.IsNullOrWhiteSpace(payload.ErrorMessage)) {
                    entity.ErrorMessage = payload.ErrorMessage;
                }
                if (!string.IsNullOrWhiteSpace(payload.ScreenshotRef)) {
                    entity.ScreenshotRef = payload.ScreenshotRef;
                }
                if (payload.ConsoleLog is not null) {
                    entity.ConsoleLog = payload.ConsoleLog;
                }
                if (payload.Status is "passed" or "failed" or "skipped") {
                    entity.FinishedAt = now;
                }
            }
            result.Add(entity);
        }

        return result;
    }

    private static SeleniumRunnerDto ToRunnerDto(SeleniumRunner r) => new(
        r.Id, r.Name, r.Location, r.MachineName, r.Version, r.Status,
        SplitTags(r.Capabilities), r.CurrentRunId, r.MaxParallel,
        r.Environment ?? string.Empty,
        r.LastHeartbeatAt, r.CreatedAt);

    /// <summary>
    /// Runner.Environment ve SeleniumRun.Environment string normalize'ı. UI hem
    /// "dev" / "staging" / "prod" hem "Development" gibi değerler gönderebiliyor;
    /// hepsini lowercased canonical forma çekiyoruz. "any" / "all" / "*" → boş string
    /// → "tüm ortamlara açık" anlamına gelir.
    /// </summary>
    private static string NormalizeEnvironment(string? value) {
        if (string.IsNullOrWhiteSpace(value)) {
            return string.Empty;
        }
        var trimmed = value.Trim().ToLowerInvariant();
        return trimmed switch {
            "any" or "all" or "*" or "default" => string.Empty,
            "development" => "dev",
            "stg" => "staging",
            "production" => "prod",
            _ => trimmed
        };
    }

    private static SeleniumRunnerDto MapRunnerDto(SeleniumRunner r) => ToRunnerDto(r);

    private static string ResolveActor(HttpContext httpContext) {
        var user = httpContext.User;
        var name = user?.Identity?.Name
                   ?? user?.Claims.FirstOrDefault(c => c.Type is "name" or "preferred_username" or "email")?.Value;
        return string.IsNullOrWhiteSpace(name) ? "System" : name;
    }

    private static async Task BroadcastRunEventAsync(NotificationHub hub, string type, string message, CancellationToken ct) {
        try {
            await hub.BroadcastAsync(new WorkItemNotificationDto(
                Ulid.NewUlid().ToGuid(),
                Guid.Empty,
                type,
                "info",
                "Selenium runner",
                message,
                "in-app",
                DateTimeOffset.UtcNow,
                null), ct);
        }
        catch {
            // best-effort
        }
    }
}
