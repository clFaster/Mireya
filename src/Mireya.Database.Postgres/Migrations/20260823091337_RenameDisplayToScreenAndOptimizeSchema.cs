using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RenameDisplayToScreenAndOptimizeSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetSyncStatuses_Displays_DisplayId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignAssignments_Displays_DisplayId",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackEvents_Displays_DisplayId",
                table: "PlaybackEvents"
            );

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssignments_CampaignId",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets"
            );

            migrationBuilder.DropIndex(
                name: "IX_AssetSyncStatuses_DisplayId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropIndex(name: "IX_Displays_Name", table: "Displays");

            migrationBuilder.DropIndex(name: "IX_Displays_ScreenIdentifier", table: "Displays");

            migrationBuilder.RenameTable(name: "Displays", newName: "Screens");

            migrationBuilder.DropIndex(name: "IX_Displays_ApprovalStatus", table: "Screens");

            migrationBuilder.DropIndex(name: "IX_Displays_IsActive", table: "Screens");

            migrationBuilder.RenameColumn(
                name: "DisplayName",
                table: "PlaybackEvents",
                newName: "ScreenName"
            );

            migrationBuilder.RenameColumn(
                name: "DisplayId",
                table: "PlaybackEvents",
                newName: "ScreenId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_PlaybackEvents_DisplayId",
                table: "PlaybackEvents",
                newName: "IX_PlaybackEvents_ScreenId"
            );

            migrationBuilder.RenameColumn(
                name: "DisplayId",
                table: "CampaignAssignments",
                newName: "ScreenId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_CampaignAssignments_DisplayId",
                table: "CampaignAssignments",
                newName: "IX_CampaignAssignments_ScreenId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_CampaignAssignments_CampaignId_DisplayId",
                table: "CampaignAssignments",
                newName: "IX_CampaignAssignments_CampaignId_ScreenId"
            );

            migrationBuilder.RenameColumn(
                name: "DisplayId",
                table: "AssetSyncStatuses",
                newName: "ScreenId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId",
                table: "AssetSyncStatuses",
                newName: "IX_AssetSyncStatuses_ScreenId_AssetId"
            );

            // Normalize legacy values before the new integrity rules are enabled.
            migrationBuilder.Sql(
                """
                UPDATE "Screens" SET "ResolutionWidth" = NULL WHERE "ResolutionWidth" <= 0;
                UPDATE "Screens" SET "ResolutionHeight" = NULL WHERE "ResolutionHeight" <= 0;
                UPDATE "Screens" SET "UserId" = NULL
                WHERE "UserId" IS NOT NULL
                  AND NOT EXISTS (SELECT 1 FROM "AspNetUsers" WHERE "AspNetUsers"."Id" = "Screens"."UserId");
                UPDATE "Screens" SET "UserId" = NULL
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (PARTITION BY "UserId" ORDER BY "UpdatedAt" DESC, "Id") AS "RowNumber"
                        FROM "Screens" WHERE "UserId" IS NOT NULL
                    ) AS "RankedUsers" WHERE "RowNumber" > 1
                );
                UPDATE "Campaigns" SET "EndDateUtc" = NULL
                WHERE "StartDateUtc" IS NOT NULL AND "EndDateUtc" IS NOT NULL AND "StartDateUtc" > "EndDateUtc";
                UPDATE "Campaigns" SET "DailyStartTime" = NULL, "DailyEndTime" = NULL
                WHERE ("DailyStartTime" IS NULL AND "DailyEndTime" IS NOT NULL)
                   OR ("DailyStartTime" IS NOT NULL AND "DailyEndTime" IS NULL);
                UPDATE "Campaigns" SET "RecurrenceDaysMask" = NULL
                WHERE "RecurrenceDaysMask" < 0 OR "RecurrenceDaysMask" > 127;
                UPDATE "CampaignAssets" SET "DurationSeconds" = NULL WHERE "DurationSeconds" <= 0;
                UPDATE "CampaignAssets"
                SET "Position" = (
                    SELECT "NewPosition" FROM (
                        SELECT "Id", CAST(ROW_NUMBER() OVER (PARTITION BY "CampaignId" ORDER BY "Position", "Id") AS INTEGER) AS "NewPosition"
                        FROM "CampaignAssets"
                    ) AS "RankedAssets" WHERE "RankedAssets"."Id" = "CampaignAssets"."Id"
                );
                UPDATE "AssetSyncStatuses"
                SET "Progress" = CASE WHEN "Progress" < 0 THEN 0 WHEN "Progress" > 100 THEN 100 ELSE "Progress" END;
                UPDATE "Assets" SET "DurationSeconds" = NULL WHERE "DurationSeconds" <= 0;
                UPDATE "Assets" SET "FileSizeBytes" = NULL WHERE "FileSizeBytes" < 0;
                """
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screens_ResolutionHeight_Positive",
                table: "Screens",
                sql: "\"ResolutionHeight\" IS NULL OR \"ResolutionHeight\" > 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Screens_ResolutionWidth_Positive",
                table: "Screens",
                sql: "\"ResolutionWidth\" IS NULL OR \"ResolutionWidth\" > 0"
            );

            migrationBuilder.Sql(
                """
                UPDATE "Campaigns"
                SET "IsDefault" = FALSE
                WHERE "Id" IN (
                    SELECT "Id"
                    FROM (
                        SELECT "Id", ROW_NUMBER() OVER (ORDER BY "UpdatedAt" DESC, "Id") AS "RowNumber"
                        FROM "Campaigns"
                        WHERE "IsDefault"
                    ) AS "RankedDefaults"
                    WHERE "RowNumber" > 1
                );
                """
            );

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_IsDefault",
                table: "Campaigns",
                column: "IsDefault",
                unique: true,
                filter: "\"IsDefault\""
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_DailyWindow_Complete",
                table: "Campaigns",
                sql: "(\"DailyStartTime\" IS NULL) = (\"DailyEndTime\" IS NULL)"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_DateRange",
                table: "Campaigns",
                sql: "\"StartDateUtc\" IS NULL OR \"EndDateUtc\" IS NULL OR \"StartDateUtc\" <= \"EndDateUtc\""
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Campaigns_RecurrenceDaysMask_Range",
                table: "Campaigns",
                sql: "\"RecurrenceDaysMask\" IS NULL OR \"RecurrenceDaysMask\" BETWEEN 0 AND 127"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignAssets_DurationSeconds_Positive",
                table: "CampaignAssets",
                sql: "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignAssets_Position_Positive",
                table: "CampaignAssets",
                sql: "\"Position\" > 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_AssetSyncStatuses_Progress_Range",
                table: "AssetSyncStatuses",
                sql: "\"Progress\" BETWEEN 0 AND 100"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assets_DurationSeconds_Positive",
                table: "Assets",
                sql: "\"DurationSeconds\" IS NULL OR \"DurationSeconds\" > 0"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_Assets_FileSizeBytes_NonNegative",
                table: "Assets",
                sql: "\"FileSizeBytes\" IS NULL OR \"FileSizeBytes\" >= 0"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Screens_ApprovalStatus_IsActive_CreatedAt",
                table: "Screens",
                columns: new[] { "ApprovalStatus", "IsActive", "CreatedAt" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_Screens_CreatedAt",
                table: "Screens",
                column: "CreatedAt"
            );

            migrationBuilder.CreateIndex(name: "IX_Screens_Name", table: "Screens", column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Screens_ScreenIdentifier",
                table: "Screens",
                column: "ScreenIdentifier",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Screens_UserId",
                table: "Screens",
                column: "UserId",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_Screens_AspNetUsers_UserId",
                table: "Screens",
                column: "UserId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AssetSyncStatuses_Screens_ScreenId",
                table: "AssetSyncStatuses",
                column: "ScreenId",
                principalTable: "Screens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignAssignments_Screens_ScreenId",
                table: "CampaignAssignments",
                column: "ScreenId",
                principalTable: "Screens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackEvents_Screens_ScreenId",
                table: "PlaybackEvents",
                column: "ScreenId",
                principalTable: "Screens",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AssetSyncStatuses_Screens_ScreenId",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_CampaignAssignments_Screens_ScreenId",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_PlaybackEvents_Screens_ScreenId",
                table: "PlaybackEvents"
            );

            migrationBuilder.DropForeignKey(
                name: "FK_Screens_AspNetUsers_UserId",
                table: "Screens"
            );

            migrationBuilder.DropIndex(
                name: "IX_Screens_ApprovalStatus_IsActive_CreatedAt",
                table: "Screens"
            );

            migrationBuilder.DropIndex(name: "IX_Screens_CreatedAt", table: "Screens");

            migrationBuilder.DropIndex(name: "IX_Screens_UserId", table: "Screens");

            migrationBuilder.DropIndex(name: "IX_Screens_Name", table: "Screens");

            migrationBuilder.DropIndex(name: "IX_Screens_ScreenIdentifier", table: "Screens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screens_ResolutionHeight_Positive",
                table: "Screens"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Screens_ResolutionWidth_Positive",
                table: "Screens"
            );

            migrationBuilder.DropIndex(name: "IX_Campaigns_IsDefault", table: "Campaigns");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_DailyWindow_Complete",
                table: "Campaigns"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_DateRange",
                table: "Campaigns"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Campaigns_RecurrenceDaysMask_Range",
                table: "Campaigns"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssets_DurationSeconds_Positive",
                table: "CampaignAssets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssets_Position_Positive",
                table: "CampaignAssets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_AssetSyncStatuses_Progress_Range",
                table: "AssetSyncStatuses"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_DurationSeconds_Positive",
                table: "Assets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_FileSizeBytes_NonNegative",
                table: "Assets"
            );

            migrationBuilder.RenameColumn(
                name: "ScreenName",
                table: "PlaybackEvents",
                newName: "DisplayName"
            );

            migrationBuilder.RenameColumn(
                name: "ScreenId",
                table: "PlaybackEvents",
                newName: "DisplayId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_PlaybackEvents_ScreenId",
                table: "PlaybackEvents",
                newName: "IX_PlaybackEvents_DisplayId"
            );

            migrationBuilder.RenameColumn(
                name: "ScreenId",
                table: "CampaignAssignments",
                newName: "DisplayId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_CampaignAssignments_ScreenId",
                table: "CampaignAssignments",
                newName: "IX_CampaignAssignments_DisplayId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_CampaignAssignments_CampaignId_ScreenId",
                table: "CampaignAssignments",
                newName: "IX_CampaignAssignments_CampaignId_DisplayId"
            );

            migrationBuilder.RenameColumn(
                name: "ScreenId",
                table: "AssetSyncStatuses",
                newName: "DisplayId"
            );

            migrationBuilder.RenameIndex(
                name: "IX_AssetSyncStatuses_ScreenId_AssetId",
                table: "AssetSyncStatuses",
                newName: "IX_AssetSyncStatuses_DisplayId_AssetId"
            );

            migrationBuilder.RenameTable(name: "Screens", newName: "Displays");

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_CampaignId",
                table: "CampaignAssignments",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_DisplayId",
                table: "AssetSyncStatuses",
                column: "DisplayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_ApprovalStatus",
                table: "Displays",
                column: "ApprovalStatus"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_IsActive",
                table: "Displays",
                column: "IsActive"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_Name",
                table: "Displays",
                column: "Name"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_ScreenIdentifier",
                table: "Displays",
                column: "ScreenIdentifier",
                unique: true
            );

            migrationBuilder.AddForeignKey(
                name: "FK_AssetSyncStatuses_Displays_DisplayId",
                table: "AssetSyncStatuses",
                column: "DisplayId",
                principalTable: "Displays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_CampaignAssignments_Displays_DisplayId",
                table: "CampaignAssignments",
                column: "DisplayId",
                principalTable: "Displays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );

            migrationBuilder.AddForeignKey(
                name: "FK_PlaybackEvents_Displays_DisplayId",
                table: "PlaybackEvents",
                column: "DisplayId",
                principalTable: "Displays",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade
            );
        }
    }
}
