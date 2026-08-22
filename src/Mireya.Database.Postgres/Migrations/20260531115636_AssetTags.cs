using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class AssetTags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Tags",
                table: "Assets",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Tags", table: "Assets");
        }
    }
}
