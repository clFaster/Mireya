using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class RemoveZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(name: "FK_Displays_Zones_ZoneId", table: "Displays");

            migrationBuilder.DropTable(name: "ZoneCampaigns");

            migrationBuilder.DropTable(name: "Zones");

            migrationBuilder.DropIndex(name: "IX_Displays_ZoneId", table: "Displays");

            migrationBuilder.DropColumn(name: "ZoneId", table: "Displays");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ZoneId",
                table: "Displays",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.CreateTable(
                name: "Zones",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Zones", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "ZoneCampaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    ZoneId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ZoneCampaigns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ZoneCampaigns_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_ZoneCampaigns_Zones_ZoneId",
                        column: x => x.ZoneId,
                        principalTable: "Zones",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_ZoneId",
                table: "Displays",
                column: "ZoneId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ZoneCampaigns_CampaignId",
                table: "ZoneCampaigns",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ZoneCampaigns_ZoneId",
                table: "ZoneCampaigns",
                column: "ZoneId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_ZoneCampaigns_ZoneId_CampaignId",
                table: "ZoneCampaigns",
                columns: new[] { "ZoneId", "CampaignId" },
                unique: true
            );

            migrationBuilder.CreateIndex(name: "IX_Zones_Name", table: "Zones", column: "Name");

            migrationBuilder.AddForeignKey(
                name: "FK_Displays_Zones_ZoneId",
                table: "Displays",
                column: "ZoneId",
                principalTable: "Zones",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull
            );
        }
    }
}
