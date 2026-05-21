using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAllowedColumnsJson : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AllowedColumnsJson",
                table: "ProjectDatabases",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AllowedColumnsJson",
                table: "ProjectDatabases");
        }
    }
}
