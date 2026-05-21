using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Data;
using SupportOpsApi.Models;
using Continuo.Observability.Attributes;
using Continuo.Shared.Pagination;

namespace SupportOpsApi.Endpoints.Handlers;

public static class SystemLogHandlers {
    public sealed record UiErrorLogInput(
        string Message,
        string? Stack,
        string? Level,
        string? Source,
        string? App,
        string? Url,
        string? Path,
        string? UserAgent,
        string? Release,
        string? UserId,
        string? UserLogin,
        string? SessionId,
        string? TenantSlug,
        string? CorrelationId,
        string? TraceId,
        DateTimeOffset? OccurredAtUtc,
        JsonElement? Tags,
        JsonElement? Extra);

    internal sealed record SystemAppLogListItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string Service,
        string Level,
        string Message,
        string? SourceContext,
        string? CorrelationId,
        string? TraceId,
        string? TenantId,
        string? UserName,
        bool HasException);

    internal sealed record SystemHttpLogListItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string Service,
        string Method,
        string Path,
        int StatusCode,
        long DurationMs,
        string? TenantId,
        string? ClientApp,
        string? InitiatorUserName,
        string? ClientIp,
        string? ClientGeoCountryCode,
        string? TargetUrl,
        string? CorrelationId,
        string? Error);

    internal sealed record DbEventHistoryListItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string Service,
        string DbContext,
        int AddedCount,
        int ModifiedCount,
        int DeletedCount,
        long? DurationMs,
        string? CorrelationId,
        string? TraceId,
        string? Error,
        string? EntitiesJson);

    internal sealed record UiErrorLogListItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string App,
        string Level,
        string Message,
        string? UserLogin,
        string? TenantSlug,
        string? Url,
        string? Path,
        string? CorrelationId,
        string? TraceId);

    public sealed record UiErrorLogDetail(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string App,
        string Source,
        string Level,
        string Message,
        string? Stack,
        string? Url,
        string? Path,
        string? UserAgent,
        string? TenantSlug,
        string? ClientApp,
        string? UserId,
        string? UserLogin,
        string? SessionId,
        string? CorrelationId,
        string? TraceId,
        string? Release,
        string? TagsJson,
        string? ExtraJson);

    internal sealed record RobotFlowHttpLogItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string Service,
        string Method,
        string Path,
        int StatusCode,
        long DurationMs,
        string? CorrelationId,
        string? TraceId,
        string? TargetService,
        string? TargetUrl,
        string? RequestBody,
        string? ResponseBody,
        string? Error);

    internal sealed record RobotFlowStepItem(
        Guid Id,
        DateTimeOffset OccurredAtUtc,
        string Service,
        string Method,
        string Path,
        int StatusCode,
        long DurationMs,
        string Severity,
        string StepType,
        string Summary,
        string? CorrelationId,
        string? TraceId,
        string? TargetService,
        string? TargetUrl,
        string? Error);

    internal sealed record RobotFlowGroupItem(
        string CorrelationId,
        DateTimeOffset? StartedAtUtc,
        DateTimeOffset? EndedAtUtc,
        IReadOnlyList<RobotFlowStepItem> Steps);

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<object>> ListUiErrorLogs(
        SupportOpsDbContext db,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string[]? app,
        string? level,
        string? correlationId,
        string? traceId,
        string? tenantSlug,
        string? userLogin,
        string? q,
        int? take,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default) {
        var query = db.UiErrorLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (app is { Length: > 0 }) {
            var apps = app.Where(a => !string.IsNullOrWhiteSpace(a)).Select(a => a.Trim()).ToArray();
            if (apps.Length > 0) {
                query = query.Where(x => apps.Contains(x.App));
            }
        }
        if (!string.IsNullOrWhiteSpace(level)) {
            var normalized = level.Trim().ToLowerInvariant();
            query = query.Where(x => x.Level.ToLower() == normalized);
        }
        if (!string.IsNullOrWhiteSpace(correlationId)) {
            query = query.Where(x => x.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(traceId)) {
            query = query.Where(x => x.TraceId == traceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantSlug)) {
            query = query.Where(x => x.TenantSlug == tenantSlug);
        }

        if (!string.IsNullOrWhiteSpace(userLogin)) {
            var normalized = userLogin.Trim().ToLowerInvariant();
            query = query.Where(x => x.UserLogin != null && x.UserLogin.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(q)) {
            query = query.Where(x =>
                x.Message.Contains(q) ||
                (x.Stack != null && x.Stack.Contains(q)) ||
                (x.Url != null && x.Url.Contains(q)) ||
                (x.Path != null && x.Path.Contains(q)) ||
                (x.UserLogin != null && x.UserLogin.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Paging.NormalizePageSize(pageSize, @default: 100, min: 1, max: 500);

        var items = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new UiErrorLogListItem(
                x.Id,
                x.OccurredAtUtc,
                x.App,
                x.Level,
                x.Message,
                x.UserLogin,
                x.TenantSlug,
                x.Url,
                x.Path,
                x.CorrelationId,
                x.TraceId))
            .ToListAsync(ct);

        return TypedResults.Ok<object>(new { items, total, page, pageSize });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<UiErrorLogDetail>, NotFound>> GetUiErrorLogById(
        SupportOpsDbContext db,
        Guid id,
        CancellationToken ct = default) {
        var item = await db.UiErrorLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item == null) {
            return TypedResults.NotFound();
        }

        var detail = new UiErrorLogDetail(
            item.Id,
            item.OccurredAtUtc,
            item.App,
            item.Source,
            item.Level,
            item.Message,
            item.Stack,
            item.Url,
            item.Path,
            item.UserAgent,
            item.TenantSlug,
            item.ClientApp,
            item.UserId,
            item.UserLogin,
            item.SessionId,
            item.CorrelationId,
            item.TraceId,
            item.Release,
            item.TagsJson,
            item.ExtraJson);

        return TypedResults.Ok(detail);
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<NoContent, BadRequest<string>>> IngestUiErrorLogs(
        SupportOpsDbContext db,
        HttpContext httpContext,
        IReadOnlyCollection<UiErrorLogInput> logs,
        CancellationToken ct = default) {
        const int MaxBatchSize = 200;
        if (logs == null || logs.Count == 0) {
            return TypedResults.BadRequest("No logs provided.");
        }
        if (logs.Count > MaxBatchSize) {
            return TypedResults.BadRequest($"Too many logs. Max batch size is {MaxBatchSize}.");
        }

        var now = DateTimeOffset.UtcNow;
        var clientAppHeader = httpContext.Request.Headers["X-Client-App"].ToString();
        var tenantHeader = httpContext.Request.Headers["X-Tenant-Slug"].ToString();
        var userAgentHeader = httpContext.Request.Headers.UserAgent.ToString();

        var entries = logs
            .Where(log => !string.IsNullOrWhiteSpace(log.Message))
            .Select(log => new UiErrorLogEntry {
                OccurredAtUtc = log.OccurredAtUtc ?? now,
                App = !string.IsNullOrWhiteSpace(log.App) ? log.App! : clientAppHeader ?? string.Empty,
                Source = string.IsNullOrWhiteSpace(log.Source) ? "client" : log.Source!,
                Level = string.IsNullOrWhiteSpace(log.Level) ? "error" : log.Level!,
                Message = log.Message,
                Stack = log.Stack,
                Url = log.Url,
                Path = log.Path,
                UserAgent = log.UserAgent ?? userAgentHeader,
                TenantSlug = log.TenantSlug ?? tenantHeader,
                ClientApp = clientAppHeader,
                UserId = log.UserId,
                UserLogin = log.UserLogin,
                SessionId = log.SessionId,
                CorrelationId = log.CorrelationId,
                TraceId = log.TraceId,
                Release = log.Release,
                TagsJson = log.Tags?.GetRawText(),
                ExtraJson = log.Extra?.GetRawText()
            })
            .ToList();

        if (entries.Count == 0) {
            return TypedResults.BadRequest("No valid log entries.");
        }

        await db.UiErrorLogs.AddRangeAsync(entries, ct);
        await db.SaveChangesAsync(ct);
        return TypedResults.NoContent();
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<object>> ListSystemHttpLogs(
        SupportOpsDbContext db,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string[]? service,
        string? severity,
        string? correlationId,
        string? transactionId,
        string? tenantId,
        string? jsonQuery,
        int? take,
        int page = 1,
        int pageSize = 100,
        string? q = null,
        CancellationToken ct = default) {
        var query = db.SystemHttpLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (service is { Length: > 0 }) {
            var services = service.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            if (services.Length > 0) {
                query = query.Where(x => services.Contains(x.Service) || (x.TargetService != null && services.Contains(x.TargetService)));
            }
        }

        var tx = !string.IsNullOrWhiteSpace(correlationId) ? correlationId : transactionId;
        if (!string.IsNullOrWhiteSpace(tx)) {
            query = query.Where(x => x.CorrelationId == tx);
        }

        if (!string.IsNullOrWhiteSpace(tenantId)) {
            query = query.Where(x => x.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(severity)) {
            severity = severity.Trim().ToLowerInvariant();
            query = severity switch {
                "error" => query.Where(x => x.StatusCode >= 500 || x.Error != null),
                "warn" => query.Where(x => x.StatusCode >= 400 && x.StatusCode < 500),
                "info" => query.Where(x => x.StatusCode < 400 && x.Error == null),
                _ => query
            };
        }

        if (!string.IsNullOrWhiteSpace(q)) {
            query = query.Where(x =>
                x.Path.Contains(q) ||
                (x.TargetUrl != null && x.TargetUrl.Contains(q)) ||
                (x.Error != null && x.Error.Contains(q)));
        }

        if (!string.IsNullOrWhiteSpace(jsonQuery)) {
            query = ApplyJsonQuery(query, jsonQuery);
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Paging.NormalizePageSize(pageSize, @default: 100, min: 1, max: 500);

        var projected = query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SystemHttpLogListItem(
                x.Id,
                x.OccurredAtUtc,
                string.IsNullOrWhiteSpace(x.TargetService) ? x.Service : x.TargetService!,
                x.Method,
                x.Path,
                x.StatusCode,
                x.DurationMs,
                x.TenantId,
                x.ClientApp,
                x.InitiatorUserName,
                x.ClientIp ?? x.RemoteIp,
                x.ClientGeoCountryCode,
                x.TargetUrl,
                x.CorrelationId,
                x.Error));

        if (take.HasValue) {
            var safeTake = Math.Clamp(take.Value, 1, 500);
            projected = projected.Take(safeTake);
        }

        var items = await projected.ToListAsync(ct);

        return TypedResults.Ok<object>(new { items, total, page, pageSize });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<object>, BadRequest<string>>> ListRobotFlow(
        SupportOpsDbContext db,
        string? orderId,
        string? paymentId,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        int? take,
        CancellationToken ct = default) {
        var normalizedOrderId = string.IsNullOrWhiteSpace(orderId) ? null : orderId.Trim();
        var normalizedPaymentId = string.IsNullOrWhiteSpace(paymentId) ? null : paymentId.Trim();
        if (string.IsNullOrWhiteSpace(normalizedOrderId) && string.IsNullOrWhiteSpace(normalizedPaymentId)) {
            return TypedResults.BadRequest("orderId or paymentId is required.");
        }

        var query = db.SystemHttpLogs.AsNoTracking().AsQueryable();
        if (fromUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (!string.IsNullOrWhiteSpace(normalizedOrderId) && !string.IsNullOrWhiteSpace(normalizedPaymentId)) {
            query = query.Where(x =>
                x.Path.Contains(normalizedOrderId) ||
                (x.TargetUrl != null && x.TargetUrl.Contains(normalizedOrderId)) ||
                (x.CorrelationId != null && x.CorrelationId.Contains(normalizedOrderId)) ||
                (x.RequestBody != null && x.RequestBody.Contains(normalizedOrderId)) ||
                (x.ResponseBody != null && x.ResponseBody.Contains(normalizedOrderId)) ||
                (x.Error != null && x.Error.Contains(normalizedOrderId)) ||
                x.Path.Contains(normalizedPaymentId) ||
                (x.TargetUrl != null && x.TargetUrl.Contains(normalizedPaymentId)) ||
                (x.CorrelationId != null && x.CorrelationId.Contains(normalizedPaymentId)) ||
                (x.RequestBody != null && x.RequestBody.Contains(normalizedPaymentId)) ||
                (x.ResponseBody != null && x.ResponseBody.Contains(normalizedPaymentId)) ||
                (x.Error != null && x.Error.Contains(normalizedPaymentId)));
        }
        else if (!string.IsNullOrWhiteSpace(normalizedOrderId)) {
            query = ApplyIdentifierFilter(query, normalizedOrderId);
        }
        else if (!string.IsNullOrWhiteSpace(normalizedPaymentId)) {
            query = ApplyIdentifierFilter(query, normalizedPaymentId);
        }

        // Keep this query focused on robot/barista/kds lifecycle traffic.
        query = query.Where(x =>
            x.Service.ToLower().Contains("robot") ||
            x.Service.ToLower().Contains("barista") ||
            x.Service.ToLower().Contains("kds") ||
            x.Path.ToLower().Contains("robot") ||
            x.Path.ToLower().Contains("barista") ||
            x.Path.ToLower().Contains("kds") ||
            x.Path.ToLower().Contains("tracking") ||
            (x.TargetService != null && (
                x.TargetService.ToLower().Contains("robot") ||
                x.TargetService.ToLower().Contains("barista") ||
                x.TargetService.ToLower().Contains("kds"))) ||
            (x.TargetUrl != null && (
                x.TargetUrl.ToLower().Contains("robot") ||
                x.TargetUrl.ToLower().Contains("barista") ||
                x.TargetUrl.ToLower().Contains("kds") ||
                x.TargetUrl.ToLower().Contains("tracking"))));

        var safeTake = Math.Clamp(take ?? 800, 50, 2000);
        var rows = await query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Take(safeTake)
            .Select(x => new RobotFlowHttpLogItem(
                x.Id,
                x.OccurredAtUtc,
                x.Service,
                x.Method,
                x.Path,
                x.StatusCode,
                x.DurationMs,
                x.CorrelationId,
                x.TraceId,
                x.TargetService,
                x.TargetUrl,
                x.RequestBody,
                x.ResponseBody,
                x.Error))
            .ToListAsync(ct);

        var orderedRows = rows
            .OrderBy(x => x.OccurredAtUtc)
            .ThenBy(x => x.Id)
            .ToList();
        var steps = orderedRows
            .Select(x => new RobotFlowStepItem(
                x.Id,
                x.OccurredAtUtc,
                x.Service,
                x.Method,
                x.Path,
                x.StatusCode,
                x.DurationMs,
                ResolveSeverity(x.StatusCode, x.Error),
                ResolveRobotStepType(x.Service, x.TargetService, x.Method, x.Path, x.StatusCode, x.Error, x.RequestBody, x.ResponseBody),
                BuildRobotSummary(x.Method, x.Path, x.StatusCode, x.DurationMs, x.Error),
                x.CorrelationId,
                x.TraceId,
                x.TargetService,
                x.TargetUrl,
                x.Error))
            .ToList();

        var groups = steps
            .GroupBy(step => ResolveFlowCorrelationId(step.CorrelationId, step.TraceId))
            .Select(group => new RobotFlowGroupItem(
                group.Key,
                group.FirstOrDefault()?.OccurredAtUtc,
                group.LastOrDefault()?.OccurredAtUtc,
                group.ToList()))
            .OrderBy(group => group.StartedAtUtc)
            .ToList();

        return TypedResults.Ok<object>(new {
            filters = new {
                orderId = normalizedOrderId,
                paymentId = normalizedPaymentId,
                fromUtc,
                toUtc
            },
            totalSteps = steps.Count,
            totalGroups = groups.Count,
            groups
        });
    }

    private static IQueryable<SystemHttpLogEntry> ApplyIdentifierFilter(IQueryable<SystemHttpLogEntry> query, string identifier) {
        return query.Where(x =>
            x.Path.Contains(identifier) ||
            (x.TargetUrl != null && x.TargetUrl.Contains(identifier)) ||
            (x.CorrelationId != null && x.CorrelationId.Contains(identifier)) ||
            (x.RequestBody != null && x.RequestBody.Contains(identifier)) ||
            (x.ResponseBody != null && x.ResponseBody.Contains(identifier)) ||
            (x.Error != null && x.Error.Contains(identifier)));
    }

    private static string ResolveFlowCorrelationId(string? correlationId, string? traceId) {
        if (!string.IsNullOrWhiteSpace(correlationId)) {
            return correlationId!;
        }

        if (!string.IsNullOrWhiteSpace(traceId)) {
            return $"trace:{traceId}";
        }

        return "no-correlation";
    }

    private static string ResolveSeverity(int statusCode, string? error) {
        if (!string.IsNullOrWhiteSpace(error) || statusCode >= 500) {
            return "error";
        }

        if (statusCode >= 400) {
            return "warn";
        }

        return "info";
    }

    private static string ResolveRobotStepType(
        string service,
        string? targetService,
        string method,
        string path,
        int statusCode,
        string? error,
        string? requestBody,
        string? responseBody) {
        var serviceLower = (service ?? string.Empty).ToLowerInvariant();
        var targetLower = (targetService ?? string.Empty).ToLowerInvariant();
        var methodUpper = (method ?? string.Empty).ToUpperInvariant();
        var pathLower = (path ?? string.Empty).ToLowerInvariant();

        if (!string.IsNullOrWhiteSpace(error) || statusCode >= 500) {
            return "RobotError";
        }

        if (pathLower.Contains("/robots/dispatch/pickup-commit", StringComparison.Ordinal)) {
            return "PickupCommit";
        }

        if (pathLower.Contains("/robots/dispatch/pickup-verify", StringComparison.Ordinal)) {
            return "PickupVerify";
        }

        if (pathLower.Contains("/robots/dispatch/deliver", StringComparison.Ordinal)) {
            return "DeliveryDispatch";
        }

        if (pathLower.Contains("/robots/tasks/", StringComparison.Ordinal) &&
            pathLower.Contains("/complete", StringComparison.Ordinal)) {
            return "DeliveryComplete";
        }

        if (pathLower.Contains("/robots/tasks", StringComparison.Ordinal)) {
            return "TaskSync";
        }

        if (pathLower.Contains("/api/sim/start", StringComparison.Ordinal)) {
            return "SimulatorStart";
        }

        if (pathLower.Contains("/api/sim/tasks", StringComparison.Ordinal)) {
            return "SimulatorTask";
        }

        if (pathLower.Contains("/api/sim/orders", StringComparison.Ordinal)) {
            return "SimulatorOrder";
        }

        if (pathLower.Contains("/barista/tasks", StringComparison.Ordinal) ||
            serviceLower.Contains("barista", StringComparison.Ordinal) ||
            targetLower.Contains("barista", StringComparison.Ordinal)) {
            return "BaristaFlow";
        }

        if (pathLower.Contains("fallback-to-kds", StringComparison.Ordinal) ||
            pathLower.Contains("/kds", StringComparison.Ordinal) ||
            serviceLower.Contains("kds", StringComparison.Ordinal) ||
            targetLower.Contains("kds", StringComparison.Ordinal)) {
            return "KdsFlow";
        }

        if (pathLower.Contains("/devices", StringComparison.Ordinal)) {
            return "DeviceLookup";
        }

        if (pathLower.Contains("/parameters/resolve", StringComparison.Ordinal)) {
            return "ParameterResolve";
        }

        if (pathLower.Contains("/tracking", StringComparison.Ordinal) ||
            requestBody?.Contains("stage", StringComparison.OrdinalIgnoreCase) == true ||
            responseBody?.Contains("stage", StringComparison.OrdinalIgnoreCase) == true) {
            return "OrderTracking";
        }

        if (methodUpper == "GET" && pathLower.Contains("/api/v2/robot/state", StringComparison.Ordinal)) {
            return "RobotStatePoll";
        }

        if (pathLower.Contains("goto", StringComparison.Ordinal) ||
            pathLower.Contains("dispatch", StringComparison.Ordinal)) {
            return "Navigation";
        }

        return "RobotStep";
    }

    private static string BuildRobotSummary(string method, string path, int statusCode, long durationMs, string? error) {
        var suffix = string.IsNullOrWhiteSpace(error) ? string.Empty : $" ({error.Split('\n')[0]})";
        return $"{method} {path} -> {statusCode} in {durationMs}ms{suffix}";
    }

    private static IQueryable<SystemHttpLogEntry> ApplyJsonQuery(IQueryable<SystemHttpLogEntry> query, string jsonQuery) {
        var expressions = jsonQuery
            .Split(new[] { "&&", " AND ", " and " }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Trim())
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        if (expressions.Length == 0) {
            return query;
        }

        foreach (var expr in expressions) {
            var match = Regex.Match(expr, @"^(?<path>[A-Za-z0-9_.]+)\s*(?<op>==|=|like)\s*(?<value>.+)$", RegexOptions.IgnoreCase);
            if (!match.Success) {
                continue;
            }

            var path = match.Groups["path"].Value.Trim();
            var op = match.Groups["op"].Value.Trim().ToLowerInvariant();
            var rawValue = match.Groups["value"].Value.Trim();
            if (rawValue.Length == 0) {
                continue;
            }

            var value = rawValue.Trim().Trim('\'', '"');
            if (op == "like") {
                value = value.Trim('%');
            }

            if (string.IsNullOrWhiteSpace(value)) {
                continue;
            }

            var fieldName = path.Split('.').LastOrDefault() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(fieldName)) {
                continue;
            }

            var pathLower = path.ToLowerInvariant();
            var targets = ResolveTargets(pathLower);
            if (targets.Length == 0) {
                continue;
            }

            var valueLower = value.ToLowerInvariant();
            var fieldLower = fieldName.ToLowerInvariant();
            var exactPattern = $"\"{fieldLower}\":\"{valueLower}\"";
            var numericPattern = $"\"{fieldLower}\":{valueLower}";

            var useLike = op == "like";
            var targetRequestHeaders = targets.Contains(JsonQueryTarget.RequestHeaders);
            var targetRequestBody = targets.Contains(JsonQueryTarget.RequestBody);
            var targetResponseHeaders = targets.Contains(JsonQueryTarget.ResponseHeaders);
            var targetResponseBody = targets.Contains(JsonQueryTarget.ResponseBody);
            var targetError = targets.Contains(JsonQueryTarget.Error);

            query = query.Where(x =>
                (targetRequestHeaders &&
                 ((x.RequestHeadersJson ?? string.Empty).ToLower().Contains(useLike ? valueLower : exactPattern) ||
                  (!useLike && (x.RequestHeadersJson ?? string.Empty).ToLower().Contains(numericPattern)))) ||
                (targetRequestBody &&
                 ((x.RequestBody ?? string.Empty).ToLower().Contains(useLike ? valueLower : exactPattern) ||
                  (!useLike && (x.RequestBody ?? string.Empty).ToLower().Contains(numericPattern)))) ||
                (targetResponseHeaders &&
                 ((x.ResponseHeadersJson ?? string.Empty).ToLower().Contains(useLike ? valueLower : exactPattern) ||
                  (!useLike && (x.ResponseHeadersJson ?? string.Empty).ToLower().Contains(numericPattern)))) ||
                (targetResponseBody &&
                 ((x.ResponseBody ?? string.Empty).ToLower().Contains(useLike ? valueLower : exactPattern) ||
                  (!useLike && (x.ResponseBody ?? string.Empty).ToLower().Contains(numericPattern)))) ||
                (targetError &&
                 ((x.Error ?? string.Empty).ToLower().Contains(useLike ? valueLower : exactPattern) ||
                  (!useLike && (x.Error ?? string.Empty).ToLower().Contains(numericPattern))))
            );
        }

        return query;
    }

    private enum JsonQueryTarget {
        RequestHeaders,
        RequestBody,
        ResponseHeaders,
        ResponseBody,
        Error
    }

    private static JsonQueryTarget[] ResolveTargets(string pathLower) {
        if (pathLower.StartsWith("request.header") || pathLower.StartsWith("request.headers")) {
            return new[] { JsonQueryTarget.RequestHeaders };
        }

        if (pathLower.StartsWith("request.body")) {
            return new[] { JsonQueryTarget.RequestBody };
        }

        if (pathLower.StartsWith("response.header") || pathLower.StartsWith("response.headers")) {
            return new[] { JsonQueryTarget.ResponseHeaders };
        }

        if (pathLower.StartsWith("response.body")) {
            return new[] { JsonQueryTarget.ResponseBody };
        }

        if (pathLower.StartsWith("error")) {
            return new[] { JsonQueryTarget.Error };
        }

        if (pathLower.StartsWith("request.")) {
            return new[] { JsonQueryTarget.RequestHeaders, JsonQueryTarget.RequestBody };
        }

        if (pathLower.StartsWith("response.")) {
            return new[] { JsonQueryTarget.ResponseHeaders, JsonQueryTarget.ResponseBody };
        }

        return Array.Empty<JsonQueryTarget>();
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<object>> ListDbEventHistory(
        SupportOpsDbContext db,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string[]? service,
        string? correlationId,
        string? transactionId,
        string? traceId,
        int? take,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default) {
        var query = db.DbEventHistory.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (service is { Length: > 0 }) {
            var services = service.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            if (services.Length > 0) {
                query = query.Where(x => services.Contains(x.Service));
            }
        }

        var tx = !string.IsNullOrWhiteSpace(correlationId) ? correlationId : transactionId;
        if (!string.IsNullOrWhiteSpace(tx)) {
            query = query.Where(x => x.CorrelationId == tx);
        }

        if (!string.IsNullOrWhiteSpace(traceId)) {
            query = query.Where(x => x.TraceId == traceId);
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Paging.NormalizePageSize(pageSize, @default: 100, min: 1, max: 500);

        var projected = query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new DbEventHistoryListItem(
                x.Id,
                x.OccurredAtUtc,
                x.Service,
                x.DbContext,
                x.AddedCount,
                x.ModifiedCount,
                x.DeletedCount,
                x.DurationMs,
                x.CorrelationId,
                x.TraceId,
                x.Error,
                x.EntitiesJson));

        if (take.HasValue) {
            var safeTake = Math.Clamp(take.Value, 1, 500);
            projected = projected.Take(safeTake);
        }

        var items = await projected.ToListAsync(ct);

        return TypedResults.Ok<object>(new { items, total, page, pageSize });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<object>, NotFound>> GetSystemHttpLogById(Guid id, SupportOpsDbContext db, CancellationToken ct) {
        var item = await db.SystemHttpLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok<object>(new {
            item.Id,
            item.OccurredAtUtc,
            item.Service,
            item.Direction,
            item.Method,
            item.Path,
            item.StatusCode,
            item.DurationMs,
            item.TenantId,
            item.ClientApp,
            item.RemoteIp,
            item.ClientIp,
            item.InitiatorUserId,
            item.InitiatorUserName,
            item.InitiatorUserEmail,
            item.ClientGeoCountryCode,
            item.ClientGeoRegion,
            item.ClientGeoCity,
            item.ClientGeoLatitude,
            item.ClientGeoLongitude,
            item.UserAgent,
            item.TargetService,
            item.TargetUrl,
            item.TraceId,
            item.SpanId,
            item.CorrelationId,
            item.RequestHeadersJson,
            item.ResponseHeadersJson,
            item.RequestBody,
            item.ResponseBody,
            item.Error
        });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Ok<object>> ListSystemAppLogs(
        SupportOpsDbContext db,
        DateTimeOffset? fromUtc,
        DateTimeOffset? toUtc,
        string[]? service,
        string? level,
        string? correlationId,
        string? traceId,
        string? tenantId,
        string? sourceContext,
        string? q,
        int? take,
        int page = 1,
        int pageSize = 100,
        CancellationToken ct = default) {
        var query = db.SystemAppLogs.AsNoTracking().AsQueryable();

        if (fromUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc >= fromUtc.Value);
        }

        if (toUtc.HasValue) {
            query = query.Where(x => x.OccurredAtUtc <= toUtc.Value);
        }

        if (service is { Length: > 0 }) {
            var services = service.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()).ToArray();
            if (services.Length > 0) {
                query = query.Where(x => services.Contains(x.Service));
            }
        }

        if (!string.IsNullOrWhiteSpace(level)) {
            var normalized = level.Trim().ToLowerInvariant();
            // Convenience: "warn" alias for the Serilog "Warning" level — UI uses warn elsewhere.
            if (normalized == "warn") {
                normalized = "warning";
            }
            query = query.Where(x => x.Level.ToLower() == normalized);
        }

        if (!string.IsNullOrWhiteSpace(correlationId)) {
            query = query.Where(x => x.CorrelationId == correlationId);
        }

        if (!string.IsNullOrWhiteSpace(traceId)) {
            query = query.Where(x => x.TraceId == traceId);
        }

        if (!string.IsNullOrWhiteSpace(tenantId)) {
            query = query.Where(x => x.TenantId == tenantId);
        }

        if (!string.IsNullOrWhiteSpace(sourceContext)) {
            query = query.Where(x => x.SourceContext != null && x.SourceContext.Contains(sourceContext));
        }

        if (!string.IsNullOrWhiteSpace(q)) {
            query = query.Where(x =>
                x.Message.Contains(q) ||
                (x.Exception != null && x.Exception.Contains(q)));
        }

        var total = await query.CountAsync(ct);
        page = Math.Max(page, 1);
        pageSize = Paging.NormalizePageSize(pageSize, @default: 100, min: 1, max: 500);

        var projected = query
            .OrderByDescending(x => x.OccurredAtUtc)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(x => new SystemAppLogListItem(
                x.Id,
                x.OccurredAtUtc,
                x.Service,
                x.Level,
                x.Message,
                x.SourceContext,
                x.CorrelationId,
                x.TraceId,
                x.TenantId,
                x.UserName,
                x.Exception != null));

        if (take.HasValue) {
            var safeTake = Math.Clamp(take.Value, 1, 500);
            projected = projected.Take(safeTake);
        }

        var items = await projected.ToListAsync(ct);
        return TypedResults.Ok<object>(new { items, total, page, pageSize });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<object>, NotFound>> GetSystemAppLogById(Guid id, SupportOpsDbContext db, CancellationToken ct) {
        var item = await db.SystemAppLogs.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok<object>(new {
            item.Id,
            item.OccurredAtUtc,
            item.Service,
            item.Level,
            item.Message,
            item.MessageTemplate,
            item.SourceContext,
            item.Exception,
            item.CorrelationId,
            item.TraceId,
            item.SpanId,
            item.TenantId,
            item.UserName,
            item.PropertiesJson
        });
    }

    [ContinuoProxyMethod("ui")]
    public static async Task<Results<Ok<object>, NotFound>> GetDbEventHistoryById(Guid id, SupportOpsDbContext db, CancellationToken ct) {
        var item = await db.DbEventHistory.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, ct);
        if (item is null) {
            return TypedResults.NotFound();
        }

        return TypedResults.Ok<object>(new {
            item.Id,
            item.OccurredAtUtc,
            item.Service,
            item.DbContext,
            item.Database,
            item.AddedCount,
            item.ModifiedCount,
            item.DeletedCount,
            item.DurationMs,
            item.CorrelationId,
            item.TraceId,
            item.SpanId,
            item.InitiatorUserId,
            item.InitiatorUserName,
            item.InitiatorUserEmail,
            item.EntitiesJson,
            item.Error
        });
    }
}
