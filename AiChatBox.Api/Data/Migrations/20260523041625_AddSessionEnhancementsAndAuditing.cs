using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionEnhancementsAndAuditing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ParentSessionId",
                table: "ChatSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SessionType",
                table: "ChatSessions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "AuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    UserId = table.Column<string>(type: "character varying(450)", maxLength: 450, nullable: false),
                    Action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Details = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuditLogs", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ParentSessionId",
                table: "ChatSessions",
                column: "ParentSessionId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_ChatSessions_ParentSessionId",
                table: "ChatSessions",
                column: "ParentSessionId",
                principalTable: "ChatSessions",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_ChatSessions_ParentSessionId",
                table: "ChatSessions");

            migrationBuilder.DropTable(
                name: "AuditLogs");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_ParentSessionId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "ParentSessionId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "SessionType",
                table: "ChatSessions");
        }
    }
}
