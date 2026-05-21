# continuo-support-ops-api

> Support & ops HTTP service — incidents, alerts, shifts, tasks, system logs, selenium e2e catalog.

Service namespace: `SupportOpsApi`. Assembly: `support-ops-api`. Postgres + SQL Server (task/doc flows on SQL Server). HtmlSanitizer for rich-text safety.

## Dependencies (6 submodules)

- `deps/continuo-shared`
- `deps/continuo-configuration`
- `deps/continuo-messaging`
- `deps/continuo-observability`
- `deps/continuo-persistence`
- `deps/continuo-coordination`

```bash
git clone --recurse-submodules https://github.com/WhiteToblack/continuo-support-ops-api.git
cd continuo-support-ops-api
dotnet build support-ops-api.sln
```

## Layout

```
src/support-ops-api/
  Program.cs
  Data/         # EF Core DbContexts (Postgres + SQL Server)
  Endpoints/    # Minimal API endpoint registrations
  Hosting/      # System log queue draining, ingestion workers
  Consumers/    # MassTransit consumers (system app log, etc.)
  Migrations/   # EF Core migrations
  Models/       # Domain models
  Services/     # Business services
  Dockerfile
  appsettings.json
```

## Note

A `selenium-catalog.json` content reference (build-time, from an external SeleniumE2E test project) was present in the upstream csproj. Removed in this fork; reintroduce explicitly if needed.

## License

Proprietary — all rights reserved.
