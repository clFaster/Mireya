using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations;

public partial class CampaignAssignmentScheduling : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(name: "IX_Campaigns_IsDefault", table: "Campaigns");
        migrationBuilder.DropCheckConstraint(
            name: "CK_Campaigns_DailyWindow_Complete",
            table: "Campaigns"
        );
        migrationBuilder.DropCheckConstraint(name: "CK_Campaigns_DateRange", table: "Campaigns");
        migrationBuilder.DropCheckConstraint(
            name: "CK_Campaigns_RecurrenceDaysMask_Range",
            table: "Campaigns"
        );
        migrationBuilder.Sql(
            """
            CREATE TABLE "CampaignAssignments_new" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CampaignAssignments" PRIMARY KEY,
                "CampaignId" TEXT NOT NULL,
                "ScreenId" TEXT NULL,
                "CreatedAt" TEXT NOT NULL,
                "UpdatedAt" TEXT NOT NULL,
                "TargetKind" INTEGER NOT NULL,
                "IsEnabled" INTEGER NOT NULL,
                "StartDateUtc" TEXT NULL,
                "EndDateUtc" TEXT NULL,
                "Priority" INTEGER NOT NULL,
                "RecurrenceDaysMask" INTEGER NULL,
                "DailyStartTime" TEXT NULL,
                "DailyEndTime" TEXT NULL,
                "RecurrenceTimeZoneId" TEXT NULL,
                CONSTRAINT "FK_CampaignAssignments_Campaigns_CampaignId" FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CampaignAssignments_Screens_ScreenId" FOREIGN KEY ("ScreenId") REFERENCES "Screens" ("Id") ON DELETE CASCADE,
                CONSTRAINT "CK_CampaignAssignments_DailyWindow_Complete" CHECK (("DailyStartTime" IS NULL) = ("DailyEndTime" IS NULL)),
                CONSTRAINT "CK_CampaignAssignments_DateRange" CHECK ("StartDateUtc" IS NULL OR "EndDateUtc" IS NULL OR "StartDateUtc" <= "EndDateUtc"),
                CONSTRAINT "CK_CampaignAssignments_RecurrenceDaysMask_Range" CHECK ("RecurrenceDaysMask" IS NULL OR "RecurrenceDaysMask" BETWEEN 0 AND 127),
                CONSTRAINT "CK_CampaignAssignments_Target" CHECK (("TargetKind" = 0 AND "ScreenId" IS NOT NULL) OR ("TargetKind" = 1 AND "ScreenId" IS NULL))
            );

            INSERT INTO "CampaignAssignments_new" (
                "Id", "CampaignId", "ScreenId", "CreatedAt", "UpdatedAt", "TargetKind",
                "IsEnabled", "StartDateUtc", "EndDateUtc", "Priority", "RecurrenceDaysMask",
                "DailyStartTime", "DailyEndTime", "RecurrenceTimeZoneId"
            )
            SELECT ca."Id", ca."CampaignId", ca."ScreenId", ca."CreatedAt", c."UpdatedAt", 0,
                   c."IsEnabled", c."StartDateUtc", c."EndDateUtc", c."Priority",
                   c."RecurrenceDaysMask", c."DailyStartTime", c."DailyEndTime", c."RecurrenceTimeZoneId"
            FROM "CampaignAssignments" ca
            INNER JOIN "Campaigns" c ON c."Id" = ca."CampaignId";

            INSERT INTO "CampaignAssignments_new" (
                "Id", "CampaignId", "ScreenId", "CreatedAt", "UpdatedAt", "TargetKind",
                "IsEnabled", "StartDateUtc", "EndDateUtc", "Priority", "RecurrenceDaysMask",
                "DailyStartTime", "DailyEndTime", "RecurrenceTimeZoneId"
            )
            SELECT lower(hex(randomblob(4))) || '-' || lower(hex(randomblob(2))) || '-4' ||
                   substr(lower(hex(randomblob(2))), 2) || '-' ||
                   substr('89ab', abs(random()) % 4 + 1, 1) || substr(lower(hex(randomblob(2))), 2) || '-' ||
                   lower(hex(randomblob(6))),
                   c."Id", NULL, c."CreatedAt", c."UpdatedAt", 1,
                   c."IsEnabled", c."StartDateUtc", c."EndDateUtc", c."Priority",
                   c."RecurrenceDaysMask", c."DailyStartTime", c."DailyEndTime", c."RecurrenceTimeZoneId"
            FROM "Campaigns" c
            WHERE c."IsDefault" = 1;

            DROP TABLE "CampaignAssignments";
            ALTER TABLE "CampaignAssignments_new" RENAME TO "CampaignAssignments";
            """
        );

        foreach (
            var column in new[]
            {
                "DailyEndTime",
                "DailyStartTime",
                "EndDateUtc",
                "IsDefault",
                "IsEnabled",
                "Priority",
                "RecurrenceDaysMask",
                "RecurrenceTimeZoneId",
                "StartDateUtc",
            }
        )
            migrationBuilder.DropColumn(name: column, table: "Campaigns");

        migrationBuilder.CreateIndex(
            name: "IX_CampaignAssignments_CampaignId_ScreenId",
            table: "CampaignAssignments",
            columns: new[] { "CampaignId", "ScreenId" },
            unique: true,
            filter: "\"TargetKind\" = 0"
        );
        migrationBuilder.CreateIndex(
            name: "IX_CampaignAssignments_ScreenId",
            table: "CampaignAssignments",
            column: "ScreenId"
        );
        migrationBuilder.CreateIndex(
            name: "IX_CampaignAssignments_TargetKind",
            table: "CampaignAssignments",
            column: "TargetKind",
            unique: true,
            filter: "\"TargetKind\" = 1"
        );
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropIndex(
            name: "IX_CampaignAssignments_CampaignId_ScreenId",
            table: "CampaignAssignments"
        );
        migrationBuilder.DropIndex(
            name: "IX_CampaignAssignments_TargetKind",
            table: "CampaignAssignments"
        );

        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyEndTime",
            table: "Campaigns",
            type: "TEXT",
            nullable: true
        );
        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyStartTime",
            table: "Campaigns",
            type: "TEXT",
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "EndDateUtc",
            table: "Campaigns",
            type: "TEXT",
            nullable: true
        );
        migrationBuilder.AddColumn<bool>(
            name: "IsDefault",
            table: "Campaigns",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.AddColumn<bool>(
            name: "IsEnabled",
            table: "Campaigns",
            type: "INTEGER",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Campaigns",
            type: "INTEGER",
            nullable: false,
            defaultValue: 0
        );
        migrationBuilder.AddColumn<int>(
            name: "RecurrenceDaysMask",
            table: "Campaigns",
            type: "INTEGER",
            nullable: true
        );
        migrationBuilder.AddColumn<string>(
            name: "RecurrenceTimeZoneId",
            table: "Campaigns",
            type: "TEXT",
            maxLength: 100,
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "StartDateUtc",
            table: "Campaigns",
            type: "TEXT",
            nullable: true
        );

        migrationBuilder.Sql(
            """
            UPDATE "Campaigns" AS c
            SET "IsDefault" = CASE WHEN EXISTS (
                    SELECT 1 FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" AND a."TargetKind" = 1
                ) THEN 1 ELSE 0 END,
                "IsEnabled" = COALESCE((SELECT a."IsEnabled" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1), 0),
                "StartDateUtc" = (SELECT a."StartDateUtc" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "EndDateUtc" = (SELECT a."EndDateUtc" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "Priority" = COALESCE((SELECT a."Priority" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1), 0),
                "RecurrenceDaysMask" = (SELECT a."RecurrenceDaysMask" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "DailyStartTime" = (SELECT a."DailyStartTime" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "DailyEndTime" = (SELECT a."DailyEndTime" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "RecurrenceTimeZoneId" = (SELECT a."RecurrenceTimeZoneId" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1);
            DELETE FROM "CampaignAssignments" WHERE "TargetKind" = 1;

            CREATE TABLE "CampaignAssignments_old" (
                "Id" TEXT NOT NULL CONSTRAINT "PK_CampaignAssignments" PRIMARY KEY,
                "CampaignId" TEXT NOT NULL,
                "ScreenId" TEXT NOT NULL,
                "CreatedAt" TEXT NOT NULL,
                CONSTRAINT "FK_CampaignAssignments_Campaigns_CampaignId" FOREIGN KEY ("CampaignId") REFERENCES "Campaigns" ("Id") ON DELETE CASCADE,
                CONSTRAINT "FK_CampaignAssignments_Screens_ScreenId" FOREIGN KEY ("ScreenId") REFERENCES "Screens" ("Id") ON DELETE CASCADE
            );

            INSERT INTO "CampaignAssignments_old" ("Id", "CampaignId", "ScreenId", "CreatedAt")
            SELECT "Id", "CampaignId", "ScreenId", "CreatedAt"
            FROM "CampaignAssignments";

            DROP TABLE "CampaignAssignments";
            ALTER TABLE "CampaignAssignments_old" RENAME TO "CampaignAssignments";
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
        migrationBuilder.CreateIndex(
            name: "IX_CampaignAssignments_CampaignId_ScreenId",
            table: "CampaignAssignments",
            columns: new[] { "CampaignId", "ScreenId" },
            unique: true
        );
        migrationBuilder.CreateIndex(
            name: "IX_CampaignAssignments_ScreenId",
            table: "CampaignAssignments",
            column: "ScreenId"
        );
    }
}
