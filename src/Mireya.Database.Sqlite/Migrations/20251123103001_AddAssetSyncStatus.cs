using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Database.Sqlite.Migrations
{
    /// <inheritdoc />
    public partial class AddAssetSyncStatus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AssetSyncStatuses",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncState = table.Column<int>(type: "INTEGER", nullable: false),
                    Progress = table.Column<int>(type: "INTEGER", nullable: false),
                    ErrorMessage = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    LastUpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssetSyncStatuses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AssetSyncStatuses_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_AssetSyncStatuses_Displays_DisplayId",
                        column: x => x.DisplayId,
                        principalTable: "Displays",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_AssetId",
                table: "AssetSyncStatuses",
                column: "AssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_DisplayId",
                table: "AssetSyncStatuses",
                column: "DisplayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_DisplayId_AssetId",
                table: "AssetSyncStatuses",
                columns: new[] { "DisplayId", "AssetId" },
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_AssetSyncStatuses_SyncState",
                table: "AssetSyncStatuses",
                column: "SyncState"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "AssetSyncStatuses");
        }
    }
}
