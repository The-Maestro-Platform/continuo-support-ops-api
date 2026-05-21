using System.Text.RegularExpressions;
using SupportOpsApi.Data;
using SupportOpsApi.Endpoints;
using SupportOpsApi.Hosting;
using SupportOpsApi.Services;
using Continuo.Configuration.Extensions;
using Continuo.Configuration.Parameters;
using Continuo.Coordination;
using Continuo.Observability;
using Continuo.Observability.Discovery;
using Continuo.Persistence;
using Continuo.Shared.Security;

var serviceName = "support-ops-api";
// Convention:
// - ConnectionStrings__pg_* -> Postgres
// - ConnectionStrings__*     -> MSSQL
var pgServiceName = "pg_SupportOps";
var tasksDbServiceName = "SupportOpsTasks"; // SQL Server for task/doc flows
var builder = Bootstrap.CreateBuilder(args, serviceName);
var configuration = builder.Configuration;

static bool LooksLikePostgres(string value) {
    var v = value.Trim();
    if (v.StartsWith("${", StringComparison.Ordinal) && v.EndsWith('}')) {
        return false;
    }

    return v.Contains("Host=", StringComparison.OrdinalIgnoreCase)
           || v.Contains("Username=", StringComparison.OrdinalIgnoreCase)
           || v.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
           || v.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase);
}

static string? FirstNonEmpty(params string?[] values)
    => values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

var envPgSupport = Environment.GetEnvironmentVariable("ConnectionStrings__pg_SupportOps");
var envConnSupport = Environment.GetEnvironmentVariable("SUPPORT_OPS_DB__CONN");
var envPostgresConnSupport = Environment.GetEnvironmentVariable("SUPPORT_OPS_PG__CONN");

var selected = FirstNonEmpty(
    !string.IsNullOrWhiteSpace(envPgSupport) && LooksLikePostgres(envPgSupport) ? envPgSupport : null,
    !string.IsNullOrWhiteSpace(envPostgresConnSupport) && LooksLikePostgres(envPostgresConnSupport) ? envPostgresConnSupport : null,
    !string.IsNullOrWhiteSpace(envConnSupport) && LooksLikePostgres(envConnSupport) ? envConnSupport : null,
    configuration.GetConnectionString(pgServiceName));

if (!string.IsNullOrWhiteSpace(selected)) {
    builder.Configuration[$"ConnectionStrings:{pgServiceName}"] = selected;
}

var resolved = builder.Configuration.GetConnectionString(pgServiceName) ?? "(null)";
Console.WriteLine($"[support-ops-api] Using Postgres connection string ({pgServiceName}): {resolved}");

static string MaskConn(string value) {
    if (string.IsNullOrWhiteSpace(value)) {
        return value;
    }

    var masked = Regex.Replace(value, @"(?i)(Password|Pwd)\s*=\s*[^;]*", "$1=***");
    return masked;
}

var resolvedTasks = PersistenceExtensions.ResolveConnectionString(builder.Configuration, tasksDbServiceName);
Console.WriteLine($"[support-ops-api] Using Tasks (MSSQL) connection string ({tasksDbServiceName}): {MaskConn(resolvedTasks)}");

builder.Services.AddContinuoServiceDiscovery(configuration, serviceName);
builder.Services.AddContinuoParameterStore(configuration);
builder.Services.AddHttpContextAccessor();
// 2026-05-14: AddAppCodeHttpClient<DevOpsReportingClient> iÃ§inde TenantHeaderHandler
// ITenantContext talep ediyor. AddTenantServices Ã§aÄŸrÄ±lmadÄ±ÄŸÄ± iÃ§in POST /selenium/runs/{id}/status
// 500 patlÄ±yor (DevOpsReportingClient inject edilirken DI fail) â†’ runner sonuÃ§larÄ±
// asla "passed/failed" olarak DB'ye yazÄ±lamÄ±yor, hep "claimed" kalÄ±yordu.
// AddTenantServices: ITenantContext + ITenantValidator + IRequestContextAccessor +
// dev-routing setup'Ä± bir kalemde yapar. Idempotent.
builder.Services.AddTenantServices(configuration, builder.Environment);
builder.Services.AddAppCodeHttpClient("support-ops-dms", "dms");
// Bridge selenium run results to devops-reporting-api so Pipeline Health reflects
// the live runner queue, not just GitHub Actions output (the only other writer).
builder.Services.AddAppCodeHttpClient<DevOpsReportingClient>("devops-reporting");
builder.Services
    .AddContinuoPostgresDbContext<SupportOpsDbContext>(configuration, pgServiceName, historySchema: "support_ops", historyTable: "__ef_migrations")
    .AddSeeder<SupportOpsSeeder>();
// Task & document flows are persisted to MSSQL per requirements
builder.Services.AddServiceDbContext<TaskFlowDbContext>(configuration, tasksDbServiceName, registerBaseBridge: false);
builder.Services.AddSingleton<DmsStorage>();

builder.Services.AddSystemHttpLogIngestion(configuration, serviceName);
builder.Services.AddSingleton<NotificationHub>();
builder.Services.AddSingleton<ServiceIncidentDetector>();
builder.Services.AddScoped<DmsApiClient>();
builder.Services.AddScoped<SlaPolicyService>();
// Replica-safe BackgroundService â€” shared coordination lock (AGENTS.md Â§13.17 T2).
builder.Services.AddPostgresLeaderLock("pg_Coordination");
builder.Services.AddHostedService<WorkItemSlaMonitor>();
builder.Services.AddMemoryCache();

builder.Services.AddContinuoMigrationHostedService<TaskFlowDbContext>(tasksDbServiceName, ensureCreatedFallback: true);
var app = Bootstrap.CreateApp(builder, serviceName);
app.UseWebSockets();

await app.Services.ApplyMigrationsOrCreateAsync<SupportOpsDbContext>(pgServiceName);
// Task/document flows use MSSQL; apply migrations on startup to keep schema in sync (Bug* metadata, comments, attachments, etc.).
await app.Services.RunSeedersAsync();

// Seed the Selenium test catalog (idempotent â€” only inserts rows whose code is not present).
using (var scope = app.Services.CreateScope()) {
    var taskDb = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
    try {
        await SeleniumCatalogSeeder.SeedAsync(taskDb);
    }
    catch (Exception seedEx) {
        Console.WriteLine($"[support-ops-api] Selenium catalog seeding skipped: {seedEx.Message}");
    }

    try {
        var swept = await SeleniumRunSweeper.SweepOnStartupAsync(taskDb, CancellationToken.None);
        if (swept > 0) {
            Console.WriteLine($"[support-ops-api] Selenium sweeper reclaimed {swept} orphaned run(s) on startup.");
        }
    }
    catch (Exception sweepEx) {
        Console.WriteLine($"[support-ops-api] Selenium startup sweep skipped: {sweepEx.Message}");
    }
}

// MapGet (not Map) so the WebSocket handshake's GET surfaces IHttpMethodMetadata.
// Without it the route is invisible to UpdateEndpointProxyFromRoutes and the
// orchestrator gateway returns 403 for the upgrade. WebSocket handshakes are
// always HTTP GET, so MapGet is the correct verb here.
app.MapGet("/support/ws/dashboard", async context => {
    if (!context.WebSockets.IsWebSocketRequest) {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required");
        return;
    }

    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var ct = context.RequestAborted;
    while (!ct.IsCancellationRequested && socket.State == System.Net.WebSockets.WebSocketState.Open) {
        var payload = System.Text.Json.JsonSerializer.Serialize(new { type = "tick", at = DateTimeOffset.UtcNow });
        var bytes = System.Text.Encoding.UTF8.GetBytes(payload);
        try {
            await socket.SendAsync(bytes, System.Net.WebSockets.WebSocketMessageType.Text, true, ct);
        }
        catch {
            break;
        }

        try {
            await Task.Delay(TimeSpan.FromSeconds(5), ct);
        }
        catch {
            break;
        }
    }
});

app.MapGet("/support/ws/notifications", async context => {
    if (!context.WebSockets.IsWebSocketRequest) {
        context.Response.StatusCode = StatusCodes.Status400BadRequest;
        await context.Response.WriteAsync("WebSocket request required");
        return;
    }

    var hub = context.RequestServices.GetRequiredService<NotificationHub>();
    using var socket = await context.WebSockets.AcceptWebSocketAsync();
    var connectionId = Guid.NewGuid();
    hub.Register(connectionId, socket);
    var buffer = new byte[1024];
    var ct = context.RequestAborted;

    try {
        while (!ct.IsCancellationRequested && socket.State == System.Net.WebSockets.WebSocketState.Open) {
            var result = await socket.ReceiveAsync(buffer, ct);
            if (result.MessageType == System.Net.WebSockets.WebSocketMessageType.Close) {
                break;
            }
        }
    }
    catch {
        // ignore
    }
    finally {
        hub.Remove(connectionId);
    }
});

app.MapSupportEndpoints();
app.MapPilotFeedbackEndpoints();
app.MapWorkItemEndpoints();
app.MapDocumentEndpoints();
app.MapSystemLogEndpoints();
app.MapSeleniumEndpoints();
app.MapCiEndpoints();
app.MapParameterDefinitionEndpoints<TaskFlowDbContext>(serviceName);

// Manifest must be generated after endpoints are mapped; otherwise only bootstrap routes (e.g. /health) get written.
app.UpdateEndpointProxyFromRoutes(serviceName, baseUrl: null, version: "v1");

app.Run();

public partial class Program;
