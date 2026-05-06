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
            migrationBuilder.Sql(
                """
                DO $$
                BEGIN
                    IF to_regclass('"AssetSyncStatuses"') IS NULL THEN
                        CREATE TABLE "AssetSyncStatuses" (
                            "Id" uuid NOT NULL,
                            "DisplayId" uuid NOT NULL,
                            "AssetId" uuid NOT NULL,
                            "SyncState" integer NOT NULL,
                            "Progress" integer NOT NULL,
                            "ErrorMessage" character varying(1000),
                            "LastUpdatedAt" timestamp with time zone NOT NULL,
                            "CreatedAt" timestamp with time zone NOT NULL,
                            CONSTRAINT "PK_AssetSyncStatuses" PRIMARY KEY ("Id"),
                            CONSTRAINT "FK_AssetSyncStatuses_Assets_AssetId" FOREIGN KEY ("AssetId") REFERENCES "Assets" ("Id") ON DELETE CASCADE,
                            CONSTRAINT "FK_AssetSyncStatuses_Displays_DisplayId" FOREIGN KEY ("DisplayId") REFERENCES "Displays" ("Id") ON DELETE CASCADE
                        );

                        CREATE INDEX "IX_AssetSyncStatuses_AssetId" ON "AssetSyncStatuses" ("AssetId");
                        CREATE INDEX "IX_AssetSyncStatuses_DisplayId" ON "AssetSyncStatuses" ("DisplayId");
                        CREATE INDEX "IX_AssetSyncStatuses_SyncState" ON "AssetSyncStatuses" ("SyncState");
                    ELSE
                        IF EXISTS (
                            SELECT 1
                            FROM information_schema.columns
                            WHERE table_schema = 'public'
                              AND table_name = 'AssetSyncStatuses'
                              AND column_name = 'CampaignId'
                        ) THEN
                            ALTER TABLE "AssetSyncStatuses" DROP CONSTRAINT IF EXISTS "FK_AssetSyncStatuses_Campaigns_CampaignId";
                            DROP INDEX IF EXISTS "IX_AssetSyncStatuses_CampaignId";
                            DROP INDEX IF EXISTS "IX_AssetSyncStatuses_DisplayId_AssetId_CampaignId";
                            ALTER TABLE "AssetSyncStatuses" DROP COLUMN IF EXISTS "CampaignId";
                        END IF;
                    END IF;

                    DROP INDEX IF EXISTS "IX_AssetSyncStatuses_DisplayId_AssetId";
                    CREATE UNIQUE INDEX "IX_AssetSyncStatuses_DisplayId_AssetId" ON "AssetSyncStatuses" ("DisplayId", "AssetId");
                END $$;
                """
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
