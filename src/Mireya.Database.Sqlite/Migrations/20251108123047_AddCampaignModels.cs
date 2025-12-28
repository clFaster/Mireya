using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddCampaignModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAssets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict
                    );
                    table.ForeignKey(
                        name: "FK_CampaignAssets_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignAssignments",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAssignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAssignments_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CampaignAssignments_Displays_DisplayId",
                        column: x => x.DisplayId,
                        principalTable: "Displays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_AssetId",
                table: "CampaignAssets",
                column: "AssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets",
                columns: new[] { "CampaignId", "Position" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_CampaignId",
                table: "CampaignAssignments",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_CampaignId_DisplayId",
                table: "CampaignAssignments",
                columns: new[] { "CampaignId", "DisplayId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_DisplayId",
                table: "CampaignAssignments",
                column: "DisplayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_CreatedAt",
                table: "Campaigns",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Name",
                table: "Campaigns",
                column: "Name"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignAssets");

            migrationBuilder.DropTable(name: "CampaignAssignments");

            migrationBuilder.DropTable(name: "Campaigns");
        }
    }
}
