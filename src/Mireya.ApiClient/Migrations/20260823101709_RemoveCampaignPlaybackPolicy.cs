using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.ApiClient.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCampaignPlaybackPolicy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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

            migrationBuilder.DropColumn(name: "DailyEndTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "DailyStartTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "EndDateUtc", table: "Campaigns");

            migrationBuilder.DropColumn(name: "IsDefault", table: "Campaigns");

            migrationBuilder.DropColumn(name: "IsEnabled", table: "Campaigns");

            migrationBuilder.DropColumn(name: "Priority", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceDaysMask", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceTimeZoneId", table: "Campaigns");

            migrationBuilder.DropColumn(name: "StartDateUtc", table: "Campaigns");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
        }
    }
}
