using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class CampaignPriority : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Priority",
                table: "Campaigns",
                type: "INTEGER",
                nullable: false,
                defaultValue: 0
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "Priority", table: "Campaigns");
        }
    }
}
