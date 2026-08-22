using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.ApiClient.Migrations
{
    /// <inheritdoc />
    public partial class ClientCampaignSchedulingPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "EndDateUtc",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );

            migrationBuilder.AddColumn<bool>(
                name: "IsEnabled",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: true
            );

            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<DateTime>(
                name: "StartDateUtc",
                table: "Campaigns",
                type: "TEXT",
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "EndDateUtc", table: "Campaigns");

            migrationBuilder.DropColumn(name: "IsEnabled", table: "Campaigns");

            migrationBuilder.DropColumn(name: "Priority", table: "Campaigns");

            migrationBuilder.DropColumn(name: "StartDateUtc", table: "Campaigns");
        }
    }
}
