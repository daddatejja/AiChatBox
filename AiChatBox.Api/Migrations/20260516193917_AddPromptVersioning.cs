using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptVersioning : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PromptTemplateVariablesJson",
                table: "Configurations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ChangeNote",
                table: "ConfigurationHistories",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DefaultProvider",
                table: "ConfigurationHistories",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PromptTemplateVariablesJson",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "ChangeNote",
                table: "ConfigurationHistories");

            migrationBuilder.DropColumn(
                name: "DefaultProvider",
                table: "ConfigurationHistories");
        }
    }
}
