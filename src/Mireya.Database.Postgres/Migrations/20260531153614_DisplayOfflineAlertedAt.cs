using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class DisplayOfflineAlertedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "OfflineAlertedAt",
                table: "Displays",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "OfflineAlertedAt",
                table: "Displays");
        }
    }
}
