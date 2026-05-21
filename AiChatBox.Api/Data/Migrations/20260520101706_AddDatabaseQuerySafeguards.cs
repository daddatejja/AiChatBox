using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDatabaseQuerySafeguards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedTables",
                table: "ProjectDatabases",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MaxQueryTimeoutSeconds",
                table: "ProjectDatabases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "MaxRecordsPerQuery",
                table: "ProjectDatabases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "SessionContextFilterJson",
                table: "ProjectDatabases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedTables",
                table: "ProjectDatabases");

            migrationBuilder.DropColumn(
                name: "MaxQueryTimeoutSeconds",
                table: "ProjectDatabases");

            migrationBuilder.DropColumn(
                name: "MaxRecordsPerQuery",
                table: "ProjectDatabases");

            migrationBuilder.DropColumn(
                name: "SessionContextFilterJson",
                table: "ProjectDatabases");
        }
    }
}
