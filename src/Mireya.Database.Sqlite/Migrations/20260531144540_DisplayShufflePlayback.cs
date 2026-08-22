using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class DisplayShufflePlayback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "ShufflePlayback",
                table: "Displays",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "ShufflePlayback", table: "Displays");
        }
    }
}
