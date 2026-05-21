using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Continuo.Observability;
using Continuo.Persistence;

namespace SupportOpsApi.Data;

public sealed class SupportOpsDbContextFactory : IDesignTimeDbContextFactory<SupportOpsDbContext> {
    public SupportOpsDbContext CreateDbContext(string[] args) {
        ContinuoEnvironment.EnsureLoaded();
        var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development";

        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{environment}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var optionsBuilder = new DbContextOptionsBuilder<SupportOpsDbContext>();
        var connectionString = PostgresExtensions.ResolvePostgresConnectionString(configuration, "pg_SupportOps");

        optionsBuilder.UseNpgsql(connectionString, npgsql => {
            npgsql.MigrationsHistoryTable("__ef_migrations", "support_ops");
        });

        var db = new SupportOpsDbContext(optionsBuilder.Options);
        MigrationHistorySeeder.EnsureHistoryBaseline(db);
        return db;
    }
}
