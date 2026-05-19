using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddMultiProviderSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AnthropicApiKey",
                table: "Configurations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderKeysJson",
                table: "Configurations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomProviderName",
                table: "Configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomProviderBaseUrl",
                table: "Configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomProviderApiKey",
                table: "Configurations",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnthropicApiKey",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "ProviderKeysJson",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderName",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderBaseUrl",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderApiKey",
                table: "Configurations");
        }
    }
}
