using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class SimplifyCampaignAssignments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssignments_DailyWindow_Complete",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssignments_RecurrenceDaysMask_Range",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropColumn(name: "DailyEndTime", table: "CampaignAssignments");

            migrationBuilder.DropColumn(name: "DailyStartTime", table: "CampaignAssignments");

            migrationBuilder.DropColumn(name: "Priority", table: "CampaignAssignments");

            migrationBuilder.DropColumn(name: "RecurrenceDaysMask", table: "CampaignAssignments");

            migrationBuilder.DropColumn(name: "RecurrenceTimeZoneId", table: "CampaignAssignments");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<TimeOnly>(
                name: "DailyEndTime",
                table: "CampaignAssignments",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<TimeOnly>(
                name: "DailyStartTime",
                table: "CampaignAssignments",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "CampaignAssignments",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<int>(
                name: "RecurrenceDaysMask",
                table: "CampaignAssignments",
                type: "INTEGER",
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "RecurrenceTimeZoneId",
                table: "CampaignAssignments",
                type: "TEXT",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignAssignments_DailyWindow_Complete",
                table: "CampaignAssignments",
                sql: "(\"DailyStartTime\" IS NULL) = (\"DailyEndTime\" IS NULL)"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignAssignments_RecurrenceDaysMask_Range",
                table: "CampaignAssignments",
                sql: "\"RecurrenceDaysMask\" IS NULL OR \"RecurrenceDaysMask\" BETWEEN 0 AND 127"
            );
        }
    }
}
