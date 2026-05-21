using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    public partial class AddSeleniumRunnerEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — `IF NOT EXISTS` ile yedek olarak prod/staging'da elle eklenmiş
            // ortamlar için de güvenli. Boş string default değer eski runner'lar (Environment
            // göndermeyen sürümler) için "tüm ortamlara açık" anlamına geliyor.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'Environment'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRunners]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRunners] ADD [Environment] nvarchar(16) NOT NULL CONSTRAINT [DF_SeleniumRunners_Environment] DEFAULT '';
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'Environment'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRuns] ADD [Environment] nvarchar(16) NULL;
                END
            ");

            // ClaimNextRun env filter'i bu kolonu sık tarıyor — Status + Environment
            // combination index queue throughput'unu korur.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_SeleniumRuns_Status_Environment'
                      AND [object_id] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    CREATE INDEX [IX_SeleniumRuns_Status_Environment]
                        ON [sup].[SeleniumRuns] ([Status], [Environment])
                        WHERE [Status] = 'queued';
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.indexes
                    WHERE [name] = N'IX_SeleniumRuns_Status_Environment'
                      AND [object_id] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    DROP INDEX [IX_SeleniumRuns_Status_Environment] ON [sup].[SeleniumRuns];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'Environment'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRuns] DROP COLUMN [Environment];
                END
            ");

            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'Environment'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRunners]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRunners] DROP CONSTRAINT IF EXISTS [DF_SeleniumRunners_Environment];
                    ALTER TABLE [sup].[SeleniumRunners] DROP COLUMN [Environment];
                END
            ");
        }
    }
}
