using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    public partial class AddSeleniumRunnerMaxParallel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent: the column might already exist in environments that were patched manually.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'MaxParallel'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRunners]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRunners] ADD [MaxParallel] int NOT NULL CONSTRAINT [DF_SeleniumRunners_MaxParallel] DEFAULT 1;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'MaxParallel'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRunners]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRunners] DROP CONSTRAINT IF EXISTS [DF_SeleniumRunners_MaxParallel];
                    ALTER TABLE [sup].[SeleniumRunners] DROP COLUMN [MaxParallel];
                END
            ");
        }
    }
}
