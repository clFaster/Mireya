using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
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
                type: "boolean",
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
