using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.ApiClient.Migrations
{
    /// <inheritdoc />
    public partial class ChangedModels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShufflePlayback",
                table: "Display",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
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

            migrationBuilder.AddColumn<bool>(
                name: "IsDefault",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
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

            migrationBuilder.AddColumn<int>(
                name: "ImageFit",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Assets",
                type: "TEXT",
                maxLength: 500,
                nullable: true
            );

            migrationBuilder.AddColumn<string>(
                name: "ThumbnailSource",
                table: "Assets",
                type: "TEXT",
                maxLength: 2000,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShufflePlayback", table: "Display");

            migrationBuilder.DropColumn(name: "DailyEndTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "DailyStartTime", table: "Campaigns");

            migrationBuilder.DropColumn(name: "IsDefault", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceDaysMask", table: "Campaigns");

            migrationBuilder.DropColumn(name: "RecurrenceTimeZoneId", table: "Campaigns");

            migrationBuilder.DropColumn(name: "ImageFit", table: "Assets");

            migrationBuilder.DropColumn(name: "Tags", table: "Assets");

            migrationBuilder.DropColumn(name: "ThumbnailSource", table: "Assets");
        }
    }
}
