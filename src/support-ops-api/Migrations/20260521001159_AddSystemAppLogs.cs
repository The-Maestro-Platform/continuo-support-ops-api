using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations.SupportOpsDb
{
    /// <inheritdoc />
    public partial class AddSystemAppLogs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SystemAppLogs",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Service = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Level = table.Column<string>(type: "character varying(24)", maxLength: 24, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    MessageTemplate = table.Column<string>(type: "text", nullable: true),
                    SourceContext = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    Exception = table.Column<string>(type: "text", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PropertiesJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemAppLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SystemAppLogs_CorrelationId",
                schema: "support_ops",
                table: "SystemAppLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAppLogs_Level",
                schema: "support_ops",
                table: "SystemAppLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAppLogs_OccurredAtUtc",
                schema: "support_ops",
                table: "SystemAppLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemAppLogs_Service_OccurredAtUtc",
                schema: "support_ops",
                table: "SystemAppLogs",
                columns: new[] { "Service", "OccurredAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SystemAppLogs",
                schema: "support_ops");
        }
    }
}
