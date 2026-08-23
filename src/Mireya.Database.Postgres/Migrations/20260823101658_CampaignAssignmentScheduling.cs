using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations;

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
        migrationBuilder.DropIndex(
            name: "IX_CampaignAssignments_CampaignId_ScreenId",
            table: "CampaignAssignments"
        );

        migrationBuilder.AlterColumn<Guid>(
            name: "ScreenId",
            table: "CampaignAssignments",
            type: "uuid",
            nullable: true,
            oldClrType: typeof(Guid),
            oldType: "uuid"
        );
        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyEndTime",
            table: "CampaignAssignments",
            type: "time without time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyStartTime",
            table: "CampaignAssignments",
            type: "time without time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "EndDateUtc",
            table: "CampaignAssignments",
            type: "timestamp with time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<bool>(
            name: "IsEnabled",
            table: "CampaignAssignments",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "CampaignAssignments",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );
        migrationBuilder.AddColumn<int>(
            name: "RecurrenceDaysMask",
            table: "CampaignAssignments",
            type: "integer",
            nullable: true
        );
        migrationBuilder.AddColumn<string>(
            name: "RecurrenceTimeZoneId",
            table: "CampaignAssignments",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "StartDateUtc",
            table: "CampaignAssignments",
            type: "timestamp with time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<int>(
            name: "TargetKind",
            table: "CampaignAssignments",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "UpdatedAt",
            table: "CampaignAssignments",
            type: "timestamp with time zone",
            nullable: false,
            defaultValue: new DateTime(1, 1, 1)
        );

        migrationBuilder.Sql(
            """
            UPDATE "CampaignAssignments" AS ca
            SET "IsEnabled" = c."IsEnabled",
                "StartDateUtc" = c."StartDateUtc",
                "EndDateUtc" = c."EndDateUtc",
                "Priority" = c."Priority",
                "RecurrenceDaysMask" = c."RecurrenceDaysMask",
                "DailyStartTime" = c."DailyStartTime",
                "DailyEndTime" = c."DailyEndTime",
                "RecurrenceTimeZoneId" = c."RecurrenceTimeZoneId",
                "TargetKind" = 0,
                "UpdatedAt" = c."UpdatedAt"
            FROM "Campaigns" AS c
            WHERE c."Id" = ca."CampaignId";

            INSERT INTO "CampaignAssignments" (
                "Id", "CampaignId", "ScreenId", "CreatedAt", "UpdatedAt", "TargetKind",
                "IsEnabled", "StartDateUtc", "EndDateUtc", "Priority", "RecurrenceDaysMask",
                "DailyStartTime", "DailyEndTime", "RecurrenceTimeZoneId"
            )
            SELECT md5(c."Id"::text || ':global-fallback')::uuid,
                   c."Id", NULL, c."CreatedAt", c."UpdatedAt", 1,
                   c."IsEnabled", c."StartDateUtc", c."EndDateUtc", c."Priority",
                   c."RecurrenceDaysMask", c."DailyStartTime", c."DailyEndTime", c."RecurrenceTimeZoneId"
            FROM "Campaigns" AS c
            WHERE c."IsDefault";
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
            name: "IX_CampaignAssignments_TargetKind",
            table: "CampaignAssignments",
            column: "TargetKind",
            unique: true,
            filter: "\"TargetKind\" = 1"
        );
        AddAssignmentConstraints(migrationBuilder);
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
        DropAssignmentConstraints(migrationBuilder);

        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyEndTime",
            table: "Campaigns",
            type: "time without time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<TimeOnly>(
            name: "DailyStartTime",
            table: "Campaigns",
            type: "time without time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "EndDateUtc",
            table: "Campaigns",
            type: "timestamp with time zone",
            nullable: true
        );
        migrationBuilder.AddColumn<bool>(
            name: "IsDefault",
            table: "Campaigns",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.AddColumn<bool>(
            name: "IsEnabled",
            table: "Campaigns",
            type: "boolean",
            nullable: false,
            defaultValue: false
        );
        migrationBuilder.AddColumn<int>(
            name: "Priority",
            table: "Campaigns",
            type: "integer",
            nullable: false,
            defaultValue: 0
        );
        migrationBuilder.AddColumn<int>(
            name: "RecurrenceDaysMask",
            table: "Campaigns",
            type: "integer",
            nullable: true
        );
        migrationBuilder.AddColumn<string>(
            name: "RecurrenceTimeZoneId",
            table: "Campaigns",
            type: "character varying(100)",
            maxLength: 100,
            nullable: true
        );
        migrationBuilder.AddColumn<DateTime>(
            name: "StartDateUtc",
            table: "Campaigns",
            type: "timestamp with time zone",
            nullable: true
        );

        migrationBuilder.Sql(
            """
            UPDATE "Campaigns" AS c
            SET "IsDefault" = EXISTS (SELECT 1 FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" AND a."TargetKind" = 1),
                "IsEnabled" = COALESCE((SELECT a."IsEnabled" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1), false),
                "StartDateUtc" = (SELECT a."StartDateUtc" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "EndDateUtc" = (SELECT a."EndDateUtc" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "Priority" = COALESCE((SELECT a."Priority" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1), 0),
                "RecurrenceDaysMask" = (SELECT a."RecurrenceDaysMask" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "DailyStartTime" = (SELECT a."DailyStartTime" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "DailyEndTime" = (SELECT a."DailyEndTime" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1),
                "RecurrenceTimeZoneId" = (SELECT a."RecurrenceTimeZoneId" FROM "CampaignAssignments" a WHERE a."CampaignId" = c."Id" ORDER BY a."TargetKind" DESC, a."CreatedAt" LIMIT 1);
            DELETE FROM "CampaignAssignments" WHERE "TargetKind" = 1;
            """
        );

        foreach (
            var column in new[]
            {
                "DailyEndTime",
                "DailyStartTime",
                "EndDateUtc",
                "IsEnabled",
                "Priority",
                "RecurrenceDaysMask",
                "RecurrenceTimeZoneId",
                "StartDateUtc",
                "TargetKind",
                "UpdatedAt",
            }
        )
            migrationBuilder.DropColumn(name: column, table: "CampaignAssignments");

        migrationBuilder.AlterColumn<Guid>(
            name: "ScreenId",
            table: "CampaignAssignments",
            type: "uuid",
            nullable: false,
            defaultValue: Guid.Empty,
            oldClrType: typeof(Guid),
            oldType: "uuid",
            oldNullable: true
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
    }

    private static void AddAssignmentConstraints(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddCheckConstraint(
            name: "CK_CampaignAssignments_DailyWindow_Complete",
            table: "CampaignAssignments",
            sql: "(\"DailyStartTime\" IS NULL) = (\"DailyEndTime\" IS NULL)"
        );
        migrationBuilder.AddCheckConstraint(
            name: "CK_CampaignAssignments_DateRange",
            table: "CampaignAssignments",
            sql: "\"StartDateUtc\" IS NULL OR \"EndDateUtc\" IS NULL OR \"StartDateUtc\" <= \"EndDateUtc\""
        );
        migrationBuilder.AddCheckConstraint(
            name: "CK_CampaignAssignments_RecurrenceDaysMask_Range",
            table: "CampaignAssignments",
            sql: "\"RecurrenceDaysMask\" IS NULL OR \"RecurrenceDaysMask\" BETWEEN 0 AND 127"
        );
        migrationBuilder.AddCheckConstraint(
            name: "CK_CampaignAssignments_Target",
            table: "CampaignAssignments",
            sql: "(\"TargetKind\" = 0 AND \"ScreenId\" IS NOT NULL) OR (\"TargetKind\" = 1 AND \"ScreenId\" IS NULL)"
        );
    }

    private static void DropAssignmentConstraints(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropCheckConstraint(
            name: "CK_CampaignAssignments_DailyWindow_Complete",
            table: "CampaignAssignments"
        );
        migrationBuilder.DropCheckConstraint(
            name: "CK_CampaignAssignments_DateRange",
            table: "CampaignAssignments"
        );
        migrationBuilder.DropCheckConstraint(
            name: "CK_CampaignAssignments_RecurrenceDaysMask_Range",
            table: "CampaignAssignments"
        );
        migrationBuilder.DropCheckConstraint(
            name: "CK_CampaignAssignments_Target",
            table: "CampaignAssignments"
        );
    }
}
