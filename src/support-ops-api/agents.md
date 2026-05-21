# Agent Development Guide — support-ops-api

## Service Overview
Support operations service managing incidents, alerts, knowledge base articles, shift assignments, and support tasks. This is a HIGH-RISK service — changes can impact incident response and operational visibility.

### Key Models
- **Alert** — System or operational alert with severity and acknowledgment status
- **Incident** — Tracked incident with priority, status, and resolution tracking
- **IncidentAction** — Action taken on an incident (escalation, note, resolution)
- **KnowledgeArticle** — Knowledge base article for support staff
- **KnowledgeRevision** — Version history for knowledge articles
- **ShiftAssignment** — Support staff shift scheduling and assignment
- **SupportTask** — Task assigned to support staff with priority and due date

### Service-Specific Notes
- Schema prefix: `support_ops.`
- **Uses PostgreSQL (NOT SQL Server)** — adjust all DB-related patterns accordingly
- Database: `ContinuoSupportOpsDb`
- HIGH-RISK: Changes to alert/incident handling directly affect operational response
- PostgreSQL differences from platform defaults:
  - No `UseCompatibilityLevel(120)` — that is SQL Server specific
  - Use `UseNpgsql()` instead of `UseSqlServer()`
  - Column naming may use snake_case per PostgreSQL conventions
  - No OPENJSON limitation — PostgreSQL has native JSON support
- Incident state transitions must be auditable — log all state changes
- Knowledge articles support revisions — never overwrite, always create new revision
- Shift assignments must not overlap for the same staff member

---

## Platform Architecture & Conventions

### Service Bootstrap
- All services use `Bootstrap.CreateBuilder(args, serviceName)` and `Bootstrap.CreateApp(builder, serviceName)` from `Continuo.Observability`
- Target framework: .NET 10 (`net10.0`), nullable enabled, implicit usings enabled
- Services reference building blocks: `Continuo.Shared`, `Continuo.Observability`, `Continuo.Messaging`, `Continuo.Persistence`

### Database & Persistence
- Use `AddContinuoPersistence(config, serviceName)` for base DB context
- Use `AddServiceDbContext<TDbContext>(config, serviceName)` for service-specific DbContext
- Connection strings resolve via `PersistenceExtensions.ResolveConnectionString()` — in containers, security-api is checked first
- DB naming convention: `Continuo{ServiceName}Db` (e.g., `ContinuoOrderDb`)
- Primary keys: ULID string `nvarchar(26)` — never use auto-increment int or GUID
- Use `ContinuoDbContext` as base class for all DbContexts
- Outbox pattern: `dbo.OutboxMessages` table for reliable event publishing
- Always use `CreatedAtUtc` (DateTime, datetime2) for timestamps — UTC only
- **PostgreSQL-specific**: Use `UseNpgsql()` provider, not `UseSqlServer()`
- Enable retry on failure: `npgsqlOptions.EnableRetryOnFailure()`
- Migrations: `MigrationRunner.ApplyMigrations<TDbContext>(app.Services, serviceName, ensureCreatedFallback: true)`
- ParameterDefinitions table: use `IParameterProvider` for configuration values

### Messaging (RabbitMQ + MassTransit)
- Use `AddContinuoMessaging(config, serviceName)` or inline `AddMassTransit` with `ConfigureRabbitMq(config, serviceName)`
- Event contracts live in `Continuo.Shared.Contracts` — reuse existing events before creating new ones
- Context propagation is automatic via MassTransit filters
- Outbox pattern ensures messages are published only after DB transaction commits
- Consumer naming: `{EventName}Consumer` in a `Consumers/` folder

### API Design
- Use Minimal API or Controllers — follow the existing pattern in the service
- All endpoints exposed via gateway must have `[ContinuoProxyMethod]` attribute
- Call `app.UpdateEndpointProxyFromRoutes(serviceName, baseUrl: null, version: "v1")` before `app.Run()`
- Return `Result<T>` for operation results, `PagedResult<T>` for lists
- Use `Paging.NormalizePageSize()` for pagination
- Error responses: common `ErrorDto` / `ErrorResponse` model
- Health endpoint: `/health` and `/healthz` by Bootstrap

### Authentication & Authorization
- JWT Bearer auth configured via `JWT:SECRET`, `JWT:ISSUER`, `JWT:AUDIENCE`
- Tenant context: `AddTenantServices()` + `app.UseTenantMiddleware()` — `ITenantContext` via DI
- Context headers: `X-Tenant-Slug`, `X-Correlation-Id`, `X-User-Id`, `X-Client-App`

### Service-to-Service Communication
- `ServiceCallExecutor` for inter-service HTTP calls (saga support)
- Named HttpClients via `AddAppCodeHttpClient("appCode", "serviceName")`
- Gateway proxy: `AddContinuoGatewayProxy(config)` — UI never calls services directly

### Observability
- Serilog (auto-configured), OpenTelemetry tracing, structured logging
- Error/Request logging to DB or file

### Code Quality & SOLID
- Single Responsibility: one bounded context per service
- Open/Closed: extend building blocks, don't modify base classes
- Interface Segregation: small, focused interfaces
- Dependency Inversion: always inject via DI, never `new` up services
- Keep controllers thin — logic in `Services/` folder
- Models in `Models/`, data in `Data/`, consumers in `Consumers/`
- Use records for DTOs and events

### Security Rules
- NEVER hardcode secrets — use config/env vars
- Validate all user input
- ALWAYS use parameterized queries or EF Core — never string concatenation in SQL
- Tenant isolation: ALWAYS filter by tenant context
- CSRF: Gateway handles double-submit cookie pattern
- HIGH-RISK: Test all incident/alert changes thoroughly — broken alerting can cause missed incidents

### Performance Rules
- async/await throughout — never `.Result` or `.Wait()`
- Use `Paging.NormalizePageSize()` — never unbounded result sets
- Keep DB transactions short, use outbox for async events
- Use optimistic concurrency for critical tables

### Testing
- Test project: `support-ops-api.Api.Tests` under `tests/`
- `public partial class Program;` at end of Program.cs for integration tests
