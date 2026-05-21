using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    public partial class AddSeleniumRunTargetRunner : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // `TargetRunnerId` kolonu önceki `20260413084049_AddSeleniumRunEnvironment` migration'ına
            // sonradan eklendiği için bazı ortamlarda kolon DB'ye işlenmeden migration applied olarak
            // kaydedildi. Idempotent raw SQL ile kolonu garanti altına alıyoruz — zaten varsa no-op.
            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'TargetRunnerId'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRuns] ADD [TargetRunnerId] uniqueidentifier NULL;
                END
            ");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'EnvironmentJson'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRuns] ADD [EnvironmentJson] nvarchar(4000) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF EXISTS (
                    SELECT 1 FROM sys.columns
                    WHERE [Name] = N'TargetRunnerId'
                      AND [Object_ID] = OBJECT_ID(N'[sup].[SeleniumRuns]')
                )
                BEGIN
                    ALTER TABLE [sup].[SeleniumRuns] DROP COLUMN [TargetRunnerId];
                END
            ");
        }
    }
}
