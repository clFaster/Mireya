using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetSyncStatuses_Campaigns_CampaignId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropIndex(
                name: "IX_AssetSyncStatuses_CampaignId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId_CampaignId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropColumn(name: "CampaignId", table: "AssetSyncStatuses");

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId",
                table: "AssetSyncStatuses",
                columns: new[] { "DisplayId", "AssetId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.AddColumn<Guid>(
                name: "CampaignId",
                table: "AssetSyncStatuses",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000")
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_CampaignId",
                table: "AssetSyncStatuses",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId_CampaignId",
                table: "AssetSyncStatuses",
                columns: new[] { "DisplayId", "AssetId", "CampaignId" },
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AssetSyncStatuses_Campaigns_CampaignId",
                table: "AssetSyncStatuses",
                column: "CampaignId",
                principalTable: "Campaigns",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
