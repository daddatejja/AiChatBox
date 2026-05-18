using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCommandRulesAndRichResponses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CommandDescription",
                table: "ConversationRules",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandName",
                table: "ConversationRules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CommandTriggerChar",
                table: "ConversationRules",
                type: "character varying(1)",
                maxLength: 1,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "ResponsePayload",
                table: "ConversationRules",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponseType",
                table: "ConversationRules",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CommandDescription",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "CommandName",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "CommandTriggerChar",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "ResponsePayload",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "ResponseType",
                table: "ConversationRules");
        }
    }
}
