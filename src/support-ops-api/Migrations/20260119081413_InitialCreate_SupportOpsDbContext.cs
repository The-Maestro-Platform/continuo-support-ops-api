using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_SupportOpsDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "support_ops");

            migrationBuilder.CreateTable(
                name: "Alerts",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Severity = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Message = table.Column<string>(type: "text", nullable: false),
                    Acknowledged = table.Column<bool>(type: "boolean", nullable: false),
                    RaisedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Alerts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DbEventHistory",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Service = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    DbContext = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Database = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    AddedCount = table.Column<int>(type: "integer", nullable: false),
                    ModifiedCount = table.Column<int>(type: "integer", nullable: false),
                    DeletedCount = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    InitiatorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InitiatorUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InitiatorUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    EntitiesJson = table.Column<string>(type: "jsonb", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DbEventHistory", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Incidents",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Priority = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    SlaMinutes = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Incidents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeArticles",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ExternalId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeArticles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ShiftAssignments",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Agent = table.Column<string>(type: "character varying(140)", maxLength: 140, nullable: false),
                    Start = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    End = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Channel = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ShiftAssignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SupportTasks",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Owner = table.Column<string>(type: "text", nullable: false),
                    Status = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    DueAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SystemHttpLogs",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    Service = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Direction = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Method = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Path = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    StatusCode = table.Column<int>(type: "integer", nullable: false),
                    DurationMs = table.Column<long>(type: "bigint", nullable: false),
                    TenantId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientApp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    RemoteIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientIp = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    InitiatorUserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    InitiatorUserName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    InitiatorUserEmail = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ClientGeoCountryCode = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: true),
                    ClientGeoRegion = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClientGeoCity = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ClientGeoLatitude = table.Column<double>(type: "double precision", nullable: true),
                    ClientGeoLongitude = table.Column<double>(type: "double precision", nullable: true),
                    TargetService = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TargetUrl = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    SpanId = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    RequestHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    ResponseHeadersJson = table.Column<string>(type: "jsonb", nullable: true),
                    RequestBody = table.Column<string>(type: "text", nullable: true),
                    ResponseBody = table.Column<string>(type: "text", nullable: true),
                    Error = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SystemHttpLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UiErrorLogs",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    OccurredAtUtc = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    App = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Source = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    Level = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    Message = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    Stack = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: true),
                    Path = table.Column<string>(type: "character varying(1024)", maxLength: 1024, nullable: true),
                    UserAgent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    TenantSlug = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    ClientApp = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    UserId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    UserLogin = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    SessionId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    CorrelationId = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    TraceId = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    Release = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    TagsJson = table.Column<string>(type: "jsonb", nullable: true),
                    ExtraJson = table.Column<string>(type: "jsonb", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UiErrorLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IncidentActions",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    IncidentId = table.Column<Guid>(type: "uuid", nullable: false),
                    ActionType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Actor = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IncidentActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IncidentActions_Incidents_IncidentId",
                        column: x => x.IncidentId,
                        principalSchema: "support_ops",
                        principalTable: "Incidents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "KnowledgeRevisions",
                schema: "support_ops",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    KnowledgeArticleId = table.Column<Guid>(type: "uuid", nullable: false),
                    Version = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnowledgeRevisions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_KnowledgeRevisions_KnowledgeArticles_KnowledgeArticleId",
                        column: x => x.KnowledgeArticleId,
                        principalSchema: "support_ops",
                        principalTable: "KnowledgeArticles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DbEventHistory_CorrelationId",
                schema: "support_ops",
                table: "DbEventHistory",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_DbEventHistory_OccurredAtUtc",
                schema: "support_ops",
                table: "DbEventHistory",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_DbEventHistory_Service_OccurredAtUtc",
                schema: "support_ops",
                table: "DbEventHistory",
                columns: new[] { "Service", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IncidentActions_IncidentId",
                schema: "support_ops",
                table: "IncidentActions",
                column: "IncidentId");

            migrationBuilder.CreateIndex(
                name: "IX_KnowledgeRevisions_KnowledgeArticleId",
                schema: "support_ops",
                table: "KnowledgeRevisions",
                column: "KnowledgeArticleId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHttpLogs_CorrelationId",
                schema: "support_ops",
                table: "SystemHttpLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHttpLogs_OccurredAtUtc",
                schema: "support_ops",
                table: "SystemHttpLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_SystemHttpLogs_Service_OccurredAtUtc",
                schema: "support_ops",
                table: "SystemHttpLogs",
                columns: new[] { "Service", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_SystemHttpLogs_StatusCode",
                schema: "support_ops",
                table: "SystemHttpLogs",
                column: "StatusCode");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_App_OccurredAtUtc",
                schema: "support_ops",
                table: "UiErrorLogs",
                columns: new[] { "App", "OccurredAtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_CorrelationId",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_Level",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "Level");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_OccurredAtUtc",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "OccurredAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_TenantSlug",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "TenantSlug");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_TraceId",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "TraceId");

            migrationBuilder.CreateIndex(
                name: "IX_UiErrorLogs_UserLogin",
                schema: "support_ops",
                table: "UiErrorLogs",
                column: "UserLogin");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Alerts",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "DbEventHistory",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "IncidentActions",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "KnowledgeRevisions",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "ShiftAssignments",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "SupportTasks",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "SystemHttpLogs",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "UiErrorLogs",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "Incidents",
                schema: "support_ops");

            migrationBuilder.DropTable(
                name: "KnowledgeArticles",
                schema: "support_ops");
        }
    }
}
