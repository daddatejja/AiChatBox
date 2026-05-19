using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Migrations
{
    /// <inheritdoc />
    public partial class AddConversationFlows : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ActiveFlowId",
                table: "ChatSessions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CurrentNodeId",
                table: "ChatSessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ConversationFlows",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProjectId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TriggerKeyword = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConversationFlows", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConversationFlows_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowEdges",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    SourceNodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetNodeId = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Condition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowEdges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowEdges_ConversationFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "FlowNodes",
                columns: table => new
                {
                    Id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FlowId = table.Column<Guid>(type: "uuid", nullable: false),
                    Type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DataJson = table.Column<string>(type: "text", nullable: false),
                    PositionX = table.Column<double>(type: "double precision", nullable: false),
                    PositionY = table.Column<double>(type: "double precision", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FlowNodes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FlowNodes_ConversationFlows_FlowId",
                        column: x => x.FlowId,
                        principalTable: "ConversationFlows",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatSessions_ActiveFlowId",
                table: "ChatSessions",
                column: "ActiveFlowId");

            migrationBuilder.CreateIndex(
                name: "IX_ConversationFlows_ProjectId",
                table: "ConversationFlows",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowEdges_FlowId",
                table: "FlowEdges",
                column: "FlowId");

            migrationBuilder.CreateIndex(
                name: "IX_FlowNodes_FlowId",
                table: "FlowNodes",
                column: "FlowId");

            migrationBuilder.AddForeignKey(
                name: "FK_ChatSessions_ConversationFlows_ActiveFlowId",
                table: "ChatSessions",
                column: "ActiveFlowId",
                principalTable: "ConversationFlows",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ChatSessions_ConversationFlows_ActiveFlowId",
                table: "ChatSessions");

            migrationBuilder.DropTable(
                name: "FlowEdges");

            migrationBuilder.DropTable(
                name: "FlowNodes");

            migrationBuilder.DropTable(
                name: "ConversationFlows");

            migrationBuilder.DropIndex(
                name: "IX_ChatSessions_ActiveFlowId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "ActiveFlowId",
                table: "ChatSessions");

            migrationBuilder.DropColumn(
                name: "CurrentNodeId",
                table: "ChatSessions");
        }
    }
}
