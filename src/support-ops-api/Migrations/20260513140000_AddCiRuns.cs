using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupportOpsApi.Data;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    [DbContext(typeof(TaskFlowDbContext))]
    [Migration("20260513140000_AddCiRuns")]
    public partial class AddCiRuns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — `IF NOT EXISTS` ile partial-applied state'lerde de güvenli.
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[sup].[CiRuns]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [sup].[CiRuns] (
                        [Id]                uniqueidentifier NOT NULL,
                        [CommitSha]         nvarchar(40)     NOT NULL,
                        [Branch]            nvarchar(160)    NOT NULL,
                        [Author]            nvarchar(160)    NOT NULL CONSTRAINT [DF_CiRuns_Author]           DEFAULT '',
                        [AuthorEmail]       nvarchar(200)    NULL,
                        [CommitMessage]     nvarchar(1000)   NULL,
                        [ChangedFilesJson]  nvarchar(8000)   NULL,
                        [ServicesInScope]   nvarchar(1000)   NOT NULL CONSTRAINT [DF_CiRuns_ServicesInScope]  DEFAULT '',
                        [Status]            nvarchar(16)     NOT NULL CONSTRAINT [DF_CiRuns_Status]           DEFAULT 'queued',
                        [StartedAt]         datetimeoffset   NOT NULL,
                        [FinishedAt]        datetimeoffset   NULL,
                        [DurationMs]        int              NOT NULL CONSTRAINT [DF_CiRuns_DurationMs]       DEFAULT 0,
                        [TotalProjects]     int              NOT NULL CONSTRAINT [DF_CiRuns_TotalProjects]    DEFAULT 0,
                        [PassedProjects]    int              NOT NULL CONSTRAINT [DF_CiRuns_PassedProjects]   DEFAULT 0,
                        [FailedProjects]    int              NOT NULL CONSTRAINT [DF_CiRuns_FailedProjects]   DEFAULT 0,
                        [TotalTests]        int              NOT NULL CONSTRAINT [DF_CiRuns_TotalTests]       DEFAULT 0,
                        [PassedTests]       int              NOT NULL CONSTRAINT [DF_CiRuns_PassedTests]      DEFAULT 0,
                        [FailedTests]       int              NOT NULL CONSTRAINT [DF_CiRuns_FailedTests]      DEFAULT 0,
                        [SkippedTests]      int              NOT NULL CONSTRAINT [DF_CiRuns_SkippedTests]     DEFAULT 0,
                        [WorkflowRunUrl]    nvarchar(500)    NULL,
                        [RunnerName]        nvarchar(160)    NULL,
                        [EnvironmentScope] nvarchar(16)     NOT NULL CONSTRAINT [DF_CiRuns_EnvironmentScope]  DEFAULT '',
                        [ErrorMessage]      nvarchar(2000)   NULL,
                        CONSTRAINT [PK_CiRuns] PRIMARY KEY ([Id])
                    );
                    CREATE INDEX [IX_CiRuns_CommitSha]        ON [sup].[CiRuns] ([CommitSha]);
                    CREATE INDEX [IX_CiRuns_Status_StartedAt] ON [sup].[CiRuns] ([Status], [StartedAt]);
                    CREATE INDEX [IX_CiRuns_Branch]           ON [sup].[CiRuns] ([Branch]);
                END
            ");

            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[sup].[CiTestProjectResults]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [sup].[CiTestProjectResults] (
                        [Id]                  uniqueidentifier NOT NULL,
                        [RunId]               uniqueidentifier NOT NULL,
                        [ProjectName]         nvarchar(200)    NOT NULL,
                        [ServiceName]         nvarchar(160)    NOT NULL CONSTRAINT [DF_CiTestProjectResults_ServiceName] DEFAULT '',
                        [Status]              nvarchar(16)     NOT NULL CONSTRAINT [DF_CiTestProjectResults_Status]      DEFAULT 'running',
                        [Passed]              int              NOT NULL CONSTRAINT [DF_CiTestProjectResults_Passed]      DEFAULT 0,
                        [Failed]              int              NOT NULL CONSTRAINT [DF_CiTestProjectResults_Failed]      DEFAULT 0,
                        [Skipped]             int              NOT NULL CONSTRAINT [DF_CiTestProjectResults_Skipped]     DEFAULT 0,
                        [DurationMs]          int              NOT NULL CONSTRAINT [DF_CiTestProjectResults_DurationMs]  DEFAULT 0,
                        [FailureDetailsJson]  nvarchar(8000)   NULL,
                        [StartedAt]           datetimeoffset   NOT NULL,
                        [FinishedAt]          datetimeoffset   NULL,
                        CONSTRAINT [PK_CiTestProjectResults] PRIMARY KEY ([Id]),
                        CONSTRAINT [FK_CiTestProjectResults_CiRuns] FOREIGN KEY ([RunId]) REFERENCES [sup].[CiRuns]([Id]) ON DELETE CASCADE
                    );
                    -- Deploy gate'in en kritik query'si: `ServiceName + StartedAt DESC limit 1`.
                    -- Indeks `ServiceName, StartedAt DESC` ordering ile PushDrivenAutoDeployWorker'ı hızlandırır.
                    CREATE INDEX [IX_CiTestProjectResults_ServiceName_StartedAt]
                        ON [sup].[CiTestProjectResults] ([ServiceName], [StartedAt] DESC);
                    CREATE INDEX [IX_CiTestProjectResults_RunId]
                        ON [sup].[CiTestProjectResults] ([RunId]);
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[sup].[CiTestProjectResults]', N'U') IS NOT NULL
                    DROP TABLE [sup].[CiTestProjectResults];
                IF OBJECT_ID(N'[sup].[CiRuns]', N'U') IS NOT NULL
                    DROP TABLE [sup].[CiRuns];
            ");
        }
    }
}
