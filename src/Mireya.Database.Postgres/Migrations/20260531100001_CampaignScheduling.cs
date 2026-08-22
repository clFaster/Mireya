using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class CampaignScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDateUtc",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Campaigns",
                type: "boolean",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateUtc",
                table: "Campaigns",
                type: "timestamp with time zone",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EndDateUtc", table: "Campaigns");

            migrationBuilder.DropColumn(name: "IsEnabled", table: "Campaigns");

            migrationBuilder.DropColumn(name: "StartDateUtc", table: "Campaigns");
        }
    }
}
