using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.ApiClient.Migrations
{
    /// <inheritdoc />
    public partial class NewModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineAlertedAt",
                table: "Display",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "Display",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Zone",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zone", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ZoneCampaign",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneCampaign", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneCampaign_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ZoneCampaign_Zone_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zone",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Display_ZoneId",
                table: "Display",
                column: "ZoneId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneCampaign_CampaignId",
                table: "ZoneCampaign",
                column: "CampaignId");

            migrationBuilder.CreateIndex(
                name: "IX_ZoneCampaign_ZoneId",
                table: "ZoneCampaign",
                column: "ZoneId");

            migrationBuilder.AddForeignKey(
                name: "FK_Display_Zone_ZoneId",
                table: "Display",
                column: "ZoneId",
                principalTable: "Zone",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Display_Zone_ZoneId",
                table: "Display");

            migrationBuilder.DropTable(
                name: "ZoneCampaign");

            migrationBuilder.DropTable(
                name: "Zone");

            migrationBuilder.DropIndex(
                name: "IX_Display_ZoneId",
                table: "Display");

            migrationBuilder.DropColumn(
                name: "OfflineAlertedAt",
                table: "Display");

            migrationBuilder.DropColumn(
                name: "ZoneId",
                table: "Display");
        }
    }
}
