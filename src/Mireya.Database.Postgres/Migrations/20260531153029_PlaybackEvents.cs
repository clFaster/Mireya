using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Postgres.Migrations
{
    /// <inheritdoc />
    public partial class PlaybackEvents : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlaybackEvents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayId = table.Column<Guid>(type: "uuid", nullable: false),
                    DisplayName = table.Column<string>(
                        type: "character varying(200)",
                        maxLength: 200,
                        nullable: false
                    ),
                    AssetId = table.Column<Guid>(type: "uuid", nullable: true),
                    AssetName = table.Column<string>(
                        type: "character varying(255)",
                        maxLength: 255,
                        nullable: true
                    ),
                    PlayedAtUtc = table.Column<DateTime>(
                        type: "timestamp with time zone",
                        nullable: false
                    ),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlaybackEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlaybackEvents_Displays_DisplayId",
                        column: x => x.DisplayId,
                        principalTable: "Displays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackEvents_AssetId",
                table: "PlaybackEvents",
                column: "AssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackEvents_DisplayId",
                table: "PlaybackEvents",
                column: "DisplayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_PlaybackEvents_PlayedAtUtc",
                table: "PlaybackEvents",
                column: "PlayedAtUtc"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "PlaybackEvents");
        }
    }
}
