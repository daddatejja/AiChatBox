using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AiChatBox.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerMultiTenancy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "EmbedSettingsJson",
                table: "Projects",
                type: "character varying(2000)",
                maxLength: 2000,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerAccountId",
                table: "Projects",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TenantIdentifier",
                table: "Projects",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "AccountType",
                table: "AspNetUsers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<Guid>(
                name: "PartnerAccountId",
                table: "AspNetUsers",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "PartnerAccounts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CompanyName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    AllowedDomainPattern = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MaxTenants = table.Column<int>(type: "integer", nullable: false),
                    CreditLimit = table.Column<decimal>(type: "numeric", nullable: false),
                    CurrentSpend = table.Column<decimal>(type: "numeric", nullable: false),
                    DefaultSystemPrompt = table.Column<string>(type: "text", nullable: true),
                    DefaultProvider = table.Column<string>(type: "text", nullable: true),
                    DefaultModel = table.Column<string>(type: "text", nullable: true),
                    DefaultThemeSettingsJson = table.Column<string>(type: "text", nullable: true),
                    MasterKeyHash = table.Column<string>(type: "text", nullable: false),
                    MasterKeyActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PartnerAccounts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Projects_PartnerAccountId",
                table: "Projects",
                column: "PartnerAccountId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_PartnerAccountId",
                table: "AspNetUsers",
                column: "PartnerAccountId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_PartnerAccounts_PartnerAccountId",
                table: "AspNetUsers",
                column: "PartnerAccountId",
                principalTable: "PartnerAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_PartnerAccounts_PartnerAccountId",
                table: "Projects",
                column: "PartnerAccountId",
                principalTable: "PartnerAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_PartnerAccounts_PartnerAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Projects_PartnerAccounts_PartnerAccountId",
                table: "Projects");

            migrationBuilder.DropTable(
                name: "PartnerAccounts");

            migrationBuilder.DropIndex(
                name: "IX_Projects_PartnerAccountId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_PartnerAccountId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "EmbedSettingsJson",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "PartnerAccountId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "TenantIdentifier",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "AccountType",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "PartnerAccountId",
                table: "AspNetUsers");
        }
    }
}
