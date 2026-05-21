using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate_TaskFlowDbContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "sup");

            migrationBuilder.CreateTable(
                name: "DmsFiles",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    ContentType = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    Length = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    StoragePath = table.Column<string>(type: "nvarchar(900)", maxLength: 900, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DmsFiles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Documents",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Owner = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Tags = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    LastUpdated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    Link = table.Column<string>(type: "nvarchar(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ParameterDefinitions",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<string>(type: "nvarchar(26)", maxLength: 26, nullable: false),
                    Module = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Section = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Key = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: false),
                    DataType = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "string"),
                    Scope = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false, defaultValue: "global"),
                    Environment = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false, defaultValue: "prod"),
                    TenantCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Locale = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    SiteCode = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FallbackValue = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MetadataJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Revision = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: false),
                    UpdatedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    CreatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    UpdatedAtUtc = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    IsSensitive = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ParameterDefinitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "WorkItems",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ExternalId = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(240)", maxLength: 240, nullable: false),
                    Type = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Status = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Priority = table.Column<string>(type: "nvarchar(8)", maxLength: 8, nullable: false),
                    Assignee = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Source = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    GithubRepo = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    GithubBranch = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: true),
                    GithubCommit = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    GithubPullRequest = table.Column<string>(type: "nvarchar(40)", maxLength: 40, nullable: true),
                    Tags = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: false),
                    BugServiceId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BugServiceName = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: true),
                    BugEndpointId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    BugEndpointPath = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    BugEndpointMethod = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SlaMinutes = table.Column<int>(type: "int", nullable: true),
                    SlaTargetAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItems", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DocumentLinks",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DocumentRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DocumentLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DocumentLinks_Documents_DocumentRecordId",
                        column: x => x.DocumentRecordId,
                        principalSchema: "sup",
                        principalTable: "Documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DocumentLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemAttachments",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FileId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DmsItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Kind = table.Column<string>(type: "nvarchar(24)", maxLength: 24, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    UploadedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_DmsFiles_FileId",
                        column: x => x.FileId,
                        principalSchema: "sup",
                        principalTable: "DmsFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WorkItemAttachments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemComments",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Author = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Text = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Format = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemComments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemComments_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemLinks",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    RelatedWorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Relation = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemLinks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemLinks_WorkItems_RelatedWorkItemId",
                        column: x => x.RelatedWorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_WorkItemLinks_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemNotifications",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Type = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Channel = table.Column<string>(type: "nvarchar(32)", maxLength: 32, nullable: false),
                    Severity = table.Column<string>(type: "nvarchar(16)", maxLength: 16, nullable: false),
                    Title = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Message = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    ReadAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemNotifications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemNotifications_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "WorkItemStatusChanges",
                schema: "sup",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    WorkItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FromStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    ToStatus = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    Actor = table.Column<string>(type: "nvarchar(160)", maxLength: 160, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(400)", maxLength: 400, nullable: true),
                    ChangedAt = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkItemStatusChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WorkItemStatusChanges_WorkItems_WorkItemId",
                        column: x => x.WorkItemId,
                        principalSchema: "sup",
                        principalTable: "WorkItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLinks_DocumentRecordId_WorkItemId",
                schema: "sup",
                table: "DocumentLinks",
                columns: new[] { "DocumentRecordId", "WorkItemId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DocumentLinks_WorkItemId",
                schema: "sup",
                table: "DocumentLinks",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_ParameterDefinitions_KeyScope",
                schema: "sup",
                table: "ParameterDefinitions",
                columns: new[] { "Module", "Section", "Key", "Environment", "Scope", "TenantCode", "Locale", "SiteCode" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_FileId",
                schema: "sup",
                table: "WorkItemAttachments",
                column: "FileId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemAttachments_WorkItemId_FileId",
                schema: "sup",
                table: "WorkItemAttachments",
                columns: new[] { "WorkItemId", "FileId" },
                unique: true,
                filter: "[FileId] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemComments_WorkItemId",
                schema: "sup",
                table: "WorkItemComments",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_RelatedWorkItemId",
                schema: "sup",
                table: "WorkItemLinks",
                column: "RelatedWorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemLinks_WorkItemId",
                schema: "sup",
                table: "WorkItemLinks",
                column: "WorkItemId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemNotifications_CreatedAt",
                schema: "sup",
                table: "WorkItemNotifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemNotifications_WorkItemId_Type",
                schema: "sup",
                table: "WorkItemNotifications",
                columns: new[] { "WorkItemId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_WorkItems_SlaTargetAt",
                schema: "sup",
                table: "WorkItems",
                column: "SlaTargetAt");

            migrationBuilder.CreateIndex(
                name: "IX_WorkItemStatusChanges_WorkItemId_ChangedAt",
                schema: "sup",
                table: "WorkItemStatusChanges",
                columns: new[] { "WorkItemId", "ChangedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DocumentLinks",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "ParameterDefinitions",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItemAttachments",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItemComments",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItemLinks",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItemNotifications",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItemStatusChanges",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "Documents",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "DmsFiles",
                schema: "sup");

            migrationBuilder.DropTable(
                name: "WorkItems",
                schema: "sup");
        }
    }
}
