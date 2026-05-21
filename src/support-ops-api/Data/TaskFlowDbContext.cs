using Microsoft.EntityFrameworkCore;
using SupportOpsApi.Models;
using Continuo.Persistence;

namespace SupportOpsApi.Data;

public class TaskFlowDbContext(DbContextOptions<TaskFlowDbContext> options) : ContinuoDbContext(options) {
    public DbSet<WorkItem> WorkItems => Set<WorkItem>();
    public DbSet<WorkItemLink> WorkItemLinks => Set<WorkItemLink>();
    public DbSet<WorkItemComment> WorkItemComments => Set<WorkItemComment>();
    public DbSet<WorkItemAttachment> WorkItemAttachments => Set<WorkItemAttachment>();
    public DbSet<WorkItemStatusChange> WorkItemStatusChanges => Set<WorkItemStatusChange>();
    public DbSet<WorkItemNotification> WorkItemNotifications => Set<WorkItemNotification>();
    public DbSet<DmsFile> DmsFiles => Set<DmsFile>();
    public DbSet<DocumentRecord> Documents => Set<DocumentRecord>();
    public DbSet<DocumentLink> DocumentLinks => Set<DocumentLink>();
    public DbSet<SeleniumTest> SeleniumTests => Set<SeleniumTest>();
    public DbSet<SeleniumFlow> SeleniumFlows => Set<SeleniumFlow>();
    public DbSet<SeleniumFlowStep> SeleniumFlowSteps => Set<SeleniumFlowStep>();
    public DbSet<SeleniumRunner> SeleniumRunners => Set<SeleniumRunner>();
    public DbSet<SeleniumRun> SeleniumRuns => Set<SeleniumRun>();
    public DbSet<SeleniumRunStep> SeleniumRunSteps => Set<SeleniumRunStep>();
    public DbSet<CiRun> CiRuns => Set<CiRun>();
    public DbSet<CiTestProjectResult> CiTestProjectResults => Set<CiTestProjectResult>();

    protected override void OnModelCreating(ModelBuilder modelBuilder) {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<WorkItem>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(240);
            entity.Property(x => x.Type).HasMaxLength(32);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.Priority).HasMaxLength(8);
            entity.Property(x => x.Assignee).HasMaxLength(160);
            entity.Property(x => x.Source).HasMaxLength(160);
            entity.Property(x => x.ExternalId).HasMaxLength(64);
            entity.Property(x => x.GithubRepo).HasMaxLength(160);
            entity.Property(x => x.GithubBranch).HasMaxLength(160);
            entity.Property(x => x.GithubCommit).HasMaxLength(80);
            entity.Property(x => x.GithubPullRequest).HasMaxLength(40);
            entity.Property(x => x.Tags).HasMaxLength(400);
            entity.Property(x => x.BugServiceName).HasMaxLength(128);
            entity.Property(x => x.BugEndpointPath).HasMaxLength(400);
            entity.Property(x => x.BugEndpointMethod).HasMaxLength(16);
            entity.Property(x => x.ResolutionNotes).HasMaxLength(2000);
            entity.HasIndex(x => x.SlaTargetAt);
            entity.HasMany(x => x.Links)
                .WithOne()
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Comments)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.Attachments)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(x => x.StatusHistory)
                .WithOne(x => x.WorkItem)
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<WorkItemLink>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Relation).HasMaxLength(64);
            entity.HasOne(x => x.RelatedWorkItem)
                .WithMany()
                .HasForeignKey(x => x.RelatedWorkItemId)
                .OnDelete(DeleteBehavior.NoAction);
        });

        modelBuilder.Entity<DocumentRecord>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(240);
            entity.Property(x => x.Category).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(64);
            entity.Property(x => x.Owner).HasMaxLength(160);
            entity.Property(x => x.Tags).HasMaxLength(400);
            entity.Property(x => x.Link).HasMaxLength(512);
            entity.HasMany(x => x.WorkItemLinks)
                .WithOne()
                .HasForeignKey(x => x.DocumentRecordId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<DocumentLink>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasIndex(x => new { x.DocumentRecordId, x.WorkItemId }).IsUnique();
        });

        modelBuilder.Entity<WorkItemComment>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Author).HasMaxLength(160);
            entity.Property(x => x.Format).HasMaxLength(16);
        });

        modelBuilder.Entity<DmsFile>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileName).HasMaxLength(260);
            entity.Property(x => x.ContentType).HasMaxLength(120);
            entity.Property(x => x.Sha256).HasMaxLength(64);
            entity.Property(x => x.StoragePath).HasMaxLength(900);
        });

        modelBuilder.Entity<WorkItemAttachment>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FileId).IsRequired(false);
            entity.Property(x => x.Kind).HasMaxLength(24);
            entity.Property(x => x.UploadedBy).HasMaxLength(160);
            entity.HasOne(x => x.File)
                .WithMany()
                .HasForeignKey(x => x.FileId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(x => new { x.WorkItemId, x.FileId }).IsUnique();
        });

        modelBuilder.Entity<WorkItemStatusChange>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.FromStatus).HasMaxLength(64);
            entity.Property(x => x.ToStatus).HasMaxLength(64);
            entity.Property(x => x.Actor).HasMaxLength(160);
            entity.Property(x => x.Note).HasMaxLength(400);
            entity.HasIndex(x => new { x.WorkItemId, x.ChangedAt });
        });

        modelBuilder.Entity<WorkItemNotification>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(64);
            entity.Property(x => x.Channel).HasMaxLength(32);
            entity.Property(x => x.Severity).HasMaxLength(16);
            entity.Property(x => x.Title).HasMaxLength(200);
            entity.Property(x => x.Message).HasMaxLength(1000);
            entity.Property(x => x.ActionsJson).HasMaxLength(4000);
            entity.HasIndex(x => new { x.WorkItemId, x.Type });
            entity.HasIndex(x => x.CreatedAt);
            entity.HasOne(x => x.WorkItem)
                .WithMany()
                .HasForeignKey(x => x.WorkItemId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeleniumTest>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.Kind).HasMaxLength(16).IsRequired();
            entity.Property(x => x.CodeFullyQualifiedName).HasMaxLength(400);
            entity.Property(x => x.ScenarioJson);
            entity.Property(x => x.Tags).HasMaxLength(400);
            entity.Property(x => x.CreatedBy).HasMaxLength(160);
        });

        modelBuilder.Entity<SeleniumFlow>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Code).HasMaxLength(120).IsRequired();
            entity.HasIndex(x => x.Code).IsUnique();
            entity.Property(x => x.Name).HasMaxLength(240).IsRequired();
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.Property(x => x.CreatedBy).HasMaxLength(160);
            entity.HasMany(x => x.Steps)
                .WithOne(x => x.Flow)
                .HasForeignKey(x => x.FlowId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeleniumFlowStep>(entity => {
            entity.HasKey(x => x.Id);
            entity.HasOne(x => x.Test)
                .WithMany()
                .HasForeignKey(x => x.TestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasIndex(x => new { x.FlowId, x.Order });
        });

        modelBuilder.Entity<SeleniumRunner>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Location).HasMaxLength(16).IsRequired();
            entity.Property(x => x.MachineName).HasMaxLength(160);
            entity.Property(x => x.Version).HasMaxLength(40);
            entity.Property(x => x.Status).HasMaxLength(16);
            entity.Property(x => x.Capabilities).HasMaxLength(400);
            entity.Property(x => x.MaxParallel).HasDefaultValue(1);
            entity.HasIndex(x => x.Name);
            entity.HasIndex(x => x.Status);
        });

        modelBuilder.Entity<SeleniumRun>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.QueuedBy).HasMaxLength(160);
            entity.Property(x => x.FailureMessage).HasMaxLength(2000);
            entity.Property(x => x.ScreenshotRef).HasMaxLength(900);
            entity.Property(x => x.EnvironmentJson).HasMaxLength(4000);
            entity.HasOne(x => x.Test)
                .WithMany()
                .HasForeignKey(x => x.TestId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne(x => x.Flow)
                .WithMany()
                .HasForeignKey(x => x.FlowId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne(x => x.Runner)
                .WithMany()
                .HasForeignKey(x => x.RunnerId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasIndex(x => new { x.Status, x.Priority, x.QueuedAt });
            entity.HasIndex(x => x.FlowBatchId);
            entity.HasMany(x => x.Steps)
                .WithOne(x => x.Run)
                .HasForeignKey(x => x.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SeleniumRunStep>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Title).HasMaxLength(400).IsRequired();
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.Property(x => x.ScreenshotRef).HasMaxLength(900);
            entity.HasIndex(x => new { x.RunId, x.Order });
        });

        modelBuilder.Entity<CiRun>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.CommitSha).HasMaxLength(40).IsRequired();
            entity.Property(x => x.Branch).HasMaxLength(160).IsRequired();
            entity.Property(x => x.Author).HasMaxLength(160);
            entity.Property(x => x.AuthorEmail).HasMaxLength(200);
            entity.Property(x => x.CommitMessage).HasMaxLength(1000);
            entity.Property(x => x.ChangedFilesJson).HasMaxLength(8000);
            entity.Property(x => x.ServicesInScope).HasMaxLength(1000);
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.WorkflowRunUrl).HasMaxLength(500);
            entity.Property(x => x.RunnerName).HasMaxLength(160);
            entity.Property(x => x.EnvironmentScope).HasMaxLength(16);
            entity.Property(x => x.ErrorMessage).HasMaxLength(2000);
            entity.HasIndex(x => x.CommitSha);
            entity.HasIndex(x => new { x.Status, x.StartedAt });
            entity.HasIndex(x => x.Branch);
            entity.HasMany(x => x.Projects)
                .WithOne(p => p.Run)
                .HasForeignKey(p => p.RunId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CiTestProjectResult>(entity => {
            entity.HasKey(x => x.Id);
            entity.Property(x => x.ProjectName).HasMaxLength(200).IsRequired();
            entity.Property(x => x.ServiceName).HasMaxLength(160);
            entity.Property(x => x.Status).HasMaxLength(16).IsRequired();
            entity.Property(x => x.FailureDetailsJson).HasMaxLength(8000);
            // CI gate sorgusu için critical index: deploy worker en son
            // `ServiceName + StartedAt DESC` ile sorgular ("auth-api son sonucu?").
            entity.HasIndex(x => new { x.ServiceName, x.StartedAt });
            entity.HasIndex(x => x.RunId);
        });
    }
}
