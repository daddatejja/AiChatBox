using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class @new : Migration
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
                name: "CustomProviderApiKey",
                table: "Configurations",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomProviderBaseUrl",
                table: "Configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CustomProviderName",
                table: "Configurations",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProviderKeysJson",
                table: "Configurations",
                type: "character varying(4000)",
                maxLength: 4000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationRules",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Trigger = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    Response = table.Column<string>(type: "text", nullable: false),
                    Priority = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationRules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationRules_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConversationRules_ProjectId",
                table: "ConversationRules",
                column: "ProjectId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "AnthropicApiKey",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderApiKey",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderBaseUrl",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "CustomProviderName",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "ProviderKeysJson",
                table: "Configurations");
        }
    }
}
