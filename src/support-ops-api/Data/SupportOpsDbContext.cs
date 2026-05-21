using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Models;

namespace SupportOpsApi.Data;

public class SupportOpsDbContext(DbContextOptions<SupportOpsDbContext> options) : DbContext(options) {
    public DbSet<Incident> Incidents => Set<Incident>();
    public DbSet<IncidentAction> IncidentActions => Set<IncidentAction>();
    public DbSet<Alert> Alerts => Set<Alert>();
    public DbSet<SupportTask> SupportTasks => Set<SupportTask>();
    public DbSet<KnowledgeArticle> KnowledgeArticles => Set<KnowledgeArticle>();
    public DbSet<KnowledgeRevision> KnowledgeRevisions => Set<KnowledgeRevision>();
    public DbSet<ShiftAssignment> ShiftAssignments => Set<ShiftAssignment>();
    public DbSet<SystemHttpLogEntry> SystemHttpLogs => Set<SystemHttpLogEntry>();
    public DbSet<SystemAppLogEntry> SystemAppLogs => Set<SystemAppLogEntry>();
    public DbSet<DbEventHistoryEntry> DbEventHistory => Set<DbEventHistoryEntry>();
    public DbSet<UiErrorLogEntry> UiErrorLogs => Set<UiErrorLogEntry>();
    public DbSet<PilotFeedback> PilotFeedbacks => Set<PilotFeedback>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        modelBuilder.HasDefaultSchema("support_ops");

        modelBuilder.Entity<Incident>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(32);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Priority).HasMaxLength(10);
            entity.Property(x => x.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<IncidentAction>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ActionType).HasMaxLength(80);
            entity.Property(x => x.Actor).HasMaxLength(120);
            entity.HasOne<Incident>()
                .WithMany(x => x.Actions)
                .HasForeignKey(x => x.IncidentId);
        });

        modelBuilder.Entity<Alert>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Source).HasMaxLength(120);
            entity.Property(x => x.Severity).HasMaxLength(32);
        });

        modelBuilder.Entity<PilotFeedback>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.PilotSite).IsRequired().HasMaxLength(80);
            entity.Property(x => x.Category).IsRequired().HasMaxLength(40);
            entity.Property(x => x.Title).IsRequired().HasMaxLength(200);
            entity.Property(x => x.Message).IsRequired();
            entity.Property(x => x.SubmittedBy).HasMaxLength(140);
            entity.Property(x => x.SubmittedRole).HasMaxLength(80);
            entity.Property(x => x.ContactEmail).HasMaxLength(200);
            entity.Property(x => x.Status).IsRequired().HasMaxLength(24);
            entity.Property(x => x.Resolution).HasMaxLength(1000);
            entity.HasIndex(x => x.CreatedAtUtc);
            entity.HasIndex(x => new { x.PilotSite, x.Status });
            entity.ToTable("PilotFeedbacks");
        });

        modelBuilder.Entity<SupportTask>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Status).HasMaxLength(32);
        });

        modelBuilder.Entity<KnowledgeArticle>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ExternalId).HasMaxLength(32);
            entity.Property(x => x.Category).HasMaxLength(120);
            entity.HasMany(x => x.Revisions)
                .WithOne()
                .HasForeignKey(x => x.KnowledgeArticleId);
        });

        modelBuilder.Entity<ShiftAssignment>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Agent).HasMaxLength(140);
            entity.Property(x => x.Channel).HasMaxLength(40);
        });

        modelBuilder.Entity<SystemHttpLogEntry>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Service).HasMaxLength(120);
            entity.Property(x => x.Direction).HasMaxLength(16);
            entity.Property(x => x.Method).HasMaxLength(16);
            entity.Property(x => x.Path).HasMaxLength(2048);
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.ClientApp).HasMaxLength(200);
            entity.Property(x => x.RemoteIp).HasMaxLength(64);
            entity.Property(x => x.ClientIp).HasMaxLength(64);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.InitiatorUserId).HasMaxLength(128);
            entity.Property(x => x.InitiatorUserName).HasMaxLength(200);
            entity.Property(x => x.InitiatorUserEmail).HasMaxLength(256);
            entity.Property(x => x.ClientGeoCountryCode).HasMaxLength(8);
            entity.Property(x => x.ClientGeoRegion).HasMaxLength(120);
            entity.Property(x => x.ClientGeoCity).HasMaxLength(120);
            entity.Property(x => x.TargetService).HasMaxLength(120);
            entity.Property(x => x.TargetUrl).HasMaxLength(2048);
            entity.Property(x => x.TraceId).HasMaxLength(64);
            entity.Property(x => x.SpanId).HasMaxLength(32);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);

            entity.Property(x => x.RequestHeadersJson).HasColumnType("jsonb");
            entity.Property(x => x.ResponseHeadersJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.Service, x.OccurredAtUtc });
            entity.HasIndex(x => x.StatusCode);
        });

        modelBuilder.Entity<SystemAppLogEntry>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Service).IsRequired().HasMaxLength(120);
            entity.Property(x => x.Level).IsRequired().HasMaxLength(24);
            entity.Property(x => x.Message).IsRequired();
            entity.Property(x => x.MessageTemplate);
            entity.Property(x => x.SourceContext).HasMaxLength(256);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.TraceId).HasMaxLength(64);
            entity.Property(x => x.SpanId).HasMaxLength(32);
            entity.Property(x => x.TenantId).HasMaxLength(64);
            entity.Property(x => x.UserName).HasMaxLength(200);
            entity.Property(x => x.PropertiesJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.Level);
            entity.HasIndex(x => new { x.Service, x.OccurredAtUtc });
        });

        modelBuilder.Entity<DbEventHistoryEntry>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Service).HasMaxLength(120);
            entity.Property(x => x.DbContext).HasMaxLength(200);
            entity.Property(x => x.Database).HasMaxLength(200);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.TraceId).HasMaxLength(64);
            entity.Property(x => x.SpanId).HasMaxLength(32);
            entity.Property(x => x.InitiatorUserId).HasMaxLength(128);
            entity.Property(x => x.InitiatorUserName).HasMaxLength(200);
            entity.Property(x => x.InitiatorUserEmail).HasMaxLength(256);
            entity.Property(x => x.EntitiesJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => new { x.Service, x.OccurredAtUtc });
        });

        modelBuilder.Entity<UiErrorLogEntry>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.App).HasMaxLength(120);
            entity.Property(x => x.Source).HasMaxLength(32);
            entity.Property(x => x.Level).HasMaxLength(16);
            entity.Property(x => x.Message).HasMaxLength(2048);
            entity.Property(x => x.Url).HasMaxLength(2048);
            entity.Property(x => x.Path).HasMaxLength(1024);
            entity.Property(x => x.UserAgent).HasMaxLength(512);
            entity.Property(x => x.TenantSlug).HasMaxLength(64);
            entity.Property(x => x.ClientApp).HasMaxLength(200);
            entity.Property(x => x.UserId).HasMaxLength(128);
            entity.Property(x => x.UserLogin).HasMaxLength(256);
            entity.Property(x => x.SessionId).HasMaxLength(128);
            entity.Property(x => x.CorrelationId).HasMaxLength(128);
            entity.Property(x => x.TraceId).HasMaxLength(64);
            entity.Property(x => x.Release).HasMaxLength(120);
            entity.Property(x => x.TagsJson).HasColumnType("jsonb");
            entity.Property(x => x.ExtraJson).HasColumnType("jsonb");

            entity.HasIndex(x => x.OccurredAtUtc);
            entity.HasIndex(x => new { x.App, x.OccurredAtUtc });
            entity.HasIndex(x => x.Level);
            entity.HasIndex(x => x.TenantSlug);
            entity.HasIndex(x => x.CorrelationId);
            entity.HasIndex(x => x.TraceId);
            entity.HasIndex(x => x.UserLogin);
        });
    }
}
