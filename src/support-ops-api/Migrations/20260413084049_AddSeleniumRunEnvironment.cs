using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace supportopsapi.Migrations.TaskFlowDb
{
    /// <inheritdoc />
    public partial class AddSeleniumRunEnvironment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EnvironmentJson",
                schema: "sup",
                table: "SeleniumRuns",
                type: "nvarchar(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "TargetRunnerId",
                schema: "sup",
                table: "SeleniumRuns",
                type: "uniqueidentifier",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetRunnerId",
                schema: "sup",
                table: "SeleniumRuns");

            migrationBuilder.DropColumn(
                name: "EnvironmentJson",
                schema: "sup",
                table: "SeleniumRuns");
        }
    }
}
