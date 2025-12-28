using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddScreenApprovalFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Displays_DeviceIdentifier", table: "Displays");

            migrationBuilder.DropColumn(name: "DeviceIdentifier", table: "Displays");

            migrationBuilder.AddColumn<int>(
                name: "ApprovalStatus",
                table: "Displays",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.AddColumn<string>(
                name: "ScreenIdentifier",
                table: "Displays",
                type: "TEXT",
                maxLength: 10,
                nullable: false,
                defaultValue: ""
            );

            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Displays",
                type: "TEXT",
                maxLength: 64,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_ApprovalStatus",
                table: "Displays",
                column: "ApprovalStatus"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_ScreenIdentifier",
                table: "Displays",
                column: "ScreenIdentifier",
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(name: "IX_Displays_ApprovalStatus", table: "Displays");

            migrationBuilder.DropIndex(name: "IX_Displays_ScreenIdentifier", table: "Displays");

            migrationBuilder.DropColumn(name: "ApprovalStatus", table: "Displays");

            migrationBuilder.DropColumn(name: "ScreenIdentifier", table: "Displays");

            migrationBuilder.DropColumn(name: "UserId", table: "Displays");

            migrationBuilder.AddColumn<string>(
                name: "DeviceIdentifier",
                table: "Displays",
                type: "TEXT",
                maxLength: 100,
                nullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_Displays_DeviceIdentifier",
                table: "Displays",
                column: "DeviceIdentifier",
                unique: true
            );
        }
    }
}
