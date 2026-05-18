using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddIntentClassification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Trigger",
                table: "ConversationRules",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<double>(
                name: "ConfidenceThreshold",
                table: "ConversationRules",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "IntentLabel",
                table: "ConversationRules",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "HandoffConfidenceThreshold",
                table: "Configurations",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "HandoffEscalationCriteria",
                table: "Configurations",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ConfidenceThreshold",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "IntentLabel",
                table: "ConversationRules");

            migrationBuilder.DropColumn(
                name: "HandoffConfidenceThreshold",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "HandoffEscalationCriteria",
                table: "Configurations");

            migrationBuilder.AlterColumn<string>(
                name: "Trigger",
                table: "ConversationRules",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(2000)",
                oldMaxLength: 2000);
        }
    }
}
