using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class RemoveCampaignFallback : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM \"CampaignAssignments\" WHERE \"TargetKind\" = 1;");

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssignments_CampaignId_ScreenId",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropIndex(
                name: "IX_CampaignAssignments_TargetKind",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropCheckConstraint(
                name: "CK_CampaignAssignments_Target",
                table: "CampaignAssignments"
            );

            migrationBuilder.DropColumn(name: "TargetKind", table: "CampaignAssignments");

            migrationBuilder.AlterColumn<Guid>(
                name: "ScreenId",
                table: "CampaignAssignments",
                type: "uuid",
                nullable: false,
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_CampaignId_ScreenId",
                table: "CampaignAssignments",
                columns: new[] { "CampaignId", "ScreenId" },
                unique: true
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_CampaignAssignments_CampaignId_ScreenId",
                table: "CampaignAssignments"
            );

            migrationBuilder.AlterColumn<Guid>(
                name: "ScreenId",
                table: "CampaignAssignments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid"
            );

            migrationBuilder.AddColumn<int>(
                name: "TargetKind",
                table: "CampaignAssignments",
                type: "integer",
                nullable: false,
                defaultValue: 0
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_CampaignId_ScreenId",
                table: "CampaignAssignments",
                columns: new[] { "CampaignId", "ScreenId" },
                unique: true,
                filter: "\"TargetKind\" = 0"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignments_TargetKind",
                table: "CampaignAssignments",
                column: "TargetKind",
                unique: true,
                filter: "\"TargetKind\" = 1"
            );

            migrationBuilder.AddCheckConstraint(
                name: "CK_CampaignAssignments_Target",
                table: "CampaignAssignments",
                sql: "(\"TargetKind\" = 0 AND \"ScreenId\" IS NOT NULL) OR (\"TargetKind\" = 1 AND \"ScreenId\" IS NULL)"
            );
        }
    }
}
