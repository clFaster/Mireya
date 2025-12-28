using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Client.Avalonia.Migrations
{
    /// <inheritdoc />
    public partial class AddIsMutedToAsset : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMuted",
                table: "Assets",
                type: "INTEGER",
                nullable: false,
                defaultValue: false
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "IsMuted", table: "Assets");
        }
    }
}
