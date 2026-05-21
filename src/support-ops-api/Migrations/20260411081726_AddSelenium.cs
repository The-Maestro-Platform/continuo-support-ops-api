using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations
{
    /// <inheritdoc />
    public partial class AddSelenium : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SeleniumFlows",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeleniumFlows", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeleniumRunners",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Location = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    MachineName = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Version = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Capabilities = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    CurrentRunId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    LastHeartbeatAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeleniumRunners", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeleniumTests",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Code = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CodeFullyQualifiedName = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ScenarioJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    TimeoutSeconds = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeleniumTests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SeleniumFlowSteps",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Order = table.Column<int>(type: "int", nullable: false),
                    TestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    StopOnFailure = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeleniumFlowSteps", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeleniumFlowSteps_SeleniumFlows_FlowId",
                        column: x => x.FlowId,
                        principalSchema: "sup",
                        principalTable: "SeleniumFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SeleniumFlowSteps_SeleniumTests_TestId",
                        column: x => x.TestId,
                        principalSchema: "sup",
                        principalTable: "SeleniumTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SeleniumRuns",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TestId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FlowId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    FlowStepOrder = table.Column<int>(type: "int", nullable: true),
                    FlowBatchId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    RunnerId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Priority = table.Column<int>(type: "int", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    QueuedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    QueuedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ClaimedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    StartedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    FinishedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DurationMs = table.Column<int>(type: "int", nullable: false),
                    Stdout = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Stderr = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FailureMessage = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    ScreenshotRef = table.Column<string>(type: "nvarchar(900)", maxLength: 900, nullable: true),
                    AutoBugWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SeleniumRuns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SeleniumRuns_SeleniumFlows_FlowId",
                        column: x => x.FlowId,
                        principalSchema: "sup",
                        principalTable: "SeleniumFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SeleniumRuns_SeleniumRunners_RunnerId",
                        column: x => x.RunnerId,
                        principalSchema: "sup",
                        principalTable: "SeleniumRunners",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SeleniumRuns_SeleniumTests_TestId",
                        column: x => x.TestId,
                        principalSchema: "sup",
                        principalTable: "SeleniumTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumFlows_Code",
                schema: "sup",
                table: "SeleniumFlows",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumFlowSteps_FlowId_Order",
                schema: "sup",
                table: "SeleniumFlowSteps",
                columns: new[] { "FlowId", "Order" });

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumFlowSteps_TestId",
                schema: "sup",
                table: "SeleniumFlowSteps",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRunners_Name",
                schema: "sup",
                table: "SeleniumRunners",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRunners_Status",
                schema: "sup",
                table: "SeleniumRunners",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRuns_FlowBatchId",
                schema: "sup",
                table: "SeleniumRuns",
                column: "FlowBatchId");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRuns_FlowId",
                schema: "sup",
                table: "SeleniumRuns",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRuns_RunnerId",
                schema: "sup",
                table: "SeleniumRuns",
                column: "RunnerId");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRuns_Status_Priority_QueuedAt",
                schema: "sup",
                table: "SeleniumRuns",
                columns: new[] { "Status", "Priority", "QueuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumRuns_TestId",
                schema: "sup",
                table: "SeleniumRuns",
                column: "TestId");

            migrationBuilder.CreateIndex(
                name: "IX_SeleniumTests_Code",
                schema: "sup",
                table: "SeleniumTests",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SeleniumFlowSteps",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "SeleniumRuns",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "SeleniumFlows",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "SeleniumRunners",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "SeleniumTests",
                schema: "sup");
        }
    }
}
