using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.ApiClient.Migrations
{
    /// <inheritdoc />
    public partial class OptimizeLocalDatabase : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "CampaignAssignment");

            migrationBuilder.DropTable(name: "Display");

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets"
            );

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets"
            );

            // Normalize legacy cache data before enabling stricter constraints.
            migrationBuilder.Sql(
                """
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
                UPDATE "Assets" SET "DurationSeconds" = NULL WHERE "DurationSeconds" <= 0;
                UPDATE "Assets" SET "FileSizeBytes" = NULL WHERE "FileSizeBytes" < 0;
                UPDATE "BackendInstances" SET "IsCurrentBackend" = FALSE
                WHERE "Id" IN (
                    SELECT "Id" FROM (
                        SELECT "Id", ROW_NUMBER() OVER (ORDER BY "LastConnectedAt" DESC, "Id") AS "RowNumber"
                        FROM "BackendInstances" WHERE "IsCurrentBackend"
                    ) AS "RankedBackends" WHERE "RowNumber" > 1
                );
                """
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

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets",
                columns: new[] { "CampaignId", "Position" },
                unique: true
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

            migrationBuilder.CreateIndex(
                name: "IX_BackendInstances_IsCurrentBackend",
                table: "BackendInstances",
                column: "IsCurrentBackend",
                unique: true,
                filter: "\"IsCurrentBackend\""
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssets_DurationSeconds_Positive",
                table: "CampaignAssets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssets_Position_Positive",
                table: "CampaignAssets"
            );

            migrationBuilder.DropIndex(
                name: "IX_BackendInstances_IsCurrentBackend",
                table: "BackendInstances"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_DurationSeconds_Positive",
                table: "Assets"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_Assets_FileSizeBytes_NonNegative",
                table: "Assets"
            );

            migrationBuilder.CreateTable(
                name: "Display",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    OfflineAlertedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    ResolutionHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ScreenIdentifier = table.Column<string>(
                        type: "TEXT",
                        maxLength: 10,
                        nullable: false
                    ),
                    ShufflePlayback = table.Column<bool>(type: "INTEGER", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Display", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAssignment_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CampaignAssignment_Display_DisplayId",
                        column: x => x.DisplayId,
                        principalTable: "Display",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets",
                columns: new[] { "CampaignId", "Position" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignment_CampaignId",
                table: "CampaignAssignment",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignment_DisplayId",
                table: "CampaignAssignment",
                column: "DisplayId"
            );
        }
    }
}
