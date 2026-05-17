using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddHumanHandoff : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "HandoffEnabled",
                table: "Configurations",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "HandoffQueueMessage",
                table: "Configurations",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffTriggerKeywords",
                table: "Configurations",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AgentId",
                table: "ChatSessions",
                type: "character varying(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "ChatSessions",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HandoffStatus",
                table: "ChatSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<DateTime>(
                name: "QueuedAt",
                table: "ChatSessions",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "HandoffEnabled",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "HandoffQueueMessage",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "HandoffTriggerKeywords",
                table: "Configurations");

            migrationBuilder.DropColumn(
                name: "AgentId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "HandoffStatus",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "QueuedAt",
                table: "ChatSessions");
        }
    }
}
