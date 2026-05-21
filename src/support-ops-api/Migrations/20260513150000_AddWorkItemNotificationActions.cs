using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SupportOpsApi.Data;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    [DbContext(typeof(TaskFlowDbContext))]
    [Migration("20260513150000_AddWorkItemNotificationActions")]
    public partial class AddWorkItemNotificationActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Idempotent — COL_LENGTH NULL ise kolon yok, ekle.
            migrationBuilder.Sql(@"
                IF COL_LENGTH('[sup].[WorkItemNotifications]', 'ActionsJson') IS NULL
                BEGIN
                    ALTER TABLE [sup].[WorkItemNotifications]
                        ADD [ActionsJson] nvarchar(4000) NULL;
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF COL_LENGTH('[sup].[WorkItemNotifications]', 'ActionsJson') IS NOT NULL
                BEGIN
                    ALTER TABLE [sup].[WorkItemNotifications] DROP COLUMN [ActionsJson];
                END
            ");
        }
    }
}
