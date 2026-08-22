using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CampaignRecurrence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "DailyEndTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "DailyStartTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceDaysMask", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceTimeZoneId", table: "Campaigns");
        }
    }
}
