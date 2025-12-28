using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Mireya.Client.Avalonia.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Assets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    Type = table.Column<int>(type: "INTEGER", nullable: false),
                    Source = table.Column<string>(type: "TEXT", maxLength: 2000, nullable: false),
                    FileSizeBytes = table.Column<long>(type: "INTEGER", nullable: true),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Assets", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "BackendInstances",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    BaseUrl = table.Column<string>(type: "TEXT", maxLength: 500, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: true),
                    IsCurrentBackend = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastConnectedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackendInstances", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Campaigns",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 1000,
                        nullable: true
                    ),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Campaigns", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "Display",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 200, nullable: false),
                    Description = table.Column<string>(
                        type: "TEXT",
                        maxLength: 500,
                        nullable: true
                    ),
                    Location = table.Column<string>(type: "TEXT", maxLength: 100, nullable: false),
                    ScreenIdentifier = table.Column<string>(
                        type: "TEXT",
                        maxLength: 10,
                        nullable: false
                    ),
                    ApprovalStatus = table.Column<int>(type: "INTEGER", nullable: false),
                    UserId = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ResolutionWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ResolutionHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    IsActive = table.Column<bool>(type: "INTEGER", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Display", x => x.Id);
                }
            );

            migrationBuilder.CreateTable(
                name: "BackendAssets",
                columns: table => new
                {
                    BackendInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_BackendAssets",
                        x => new { x.BackendInstanceId, x.AssetId }
                    );
                    table.ForeignKey(
                        name: "FK_BackendAssets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_BackendAssets_BackendInstances_BackendInstanceId",
                        column: x => x.BackendInstanceId,
                        principalTable: "BackendInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "BackendCredentials",
                columns: table => new
                {
                    BackendInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Username = table.Column<string>(type: "TEXT", nullable: true),
                    EncryptedAccessToken = table.Column<byte[]>(type: "BLOB", nullable: true),
                    EncryptedRefreshToken = table.Column<byte[]>(type: "BLOB", nullable: true),
                    TokenExpiresAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BackendCredentials", x => x.BackendInstanceId);
                    table.ForeignKey(
                        name: "FK_BackendCredentials_BackendInstances_BackendInstanceId",
                        column: x => x.BackendInstanceId,
                        principalTable: "BackendInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "DownloadedAssets",
                columns: table => new
                {
                    BackendInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    LocalPath = table.Column<string>(type: "TEXT", maxLength: 500, nullable: true),
                    FileExtension = table.Column<string>(
                        type: "TEXT",
                        maxLength: 10,
                        nullable: true
                    ),
                    IsDownloaded = table.Column<bool>(type: "INTEGER", nullable: false),
                    DownloadedAt = table.Column<DateTime>(type: "TEXT", nullable: true),
                    LastCheckedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_DownloadedAssets",
                        x => new { x.BackendInstanceId, x.AssetId }
                    );
                    table.ForeignKey(
                        name: "FK_DownloadedAssets_BackendInstances_BackendInstanceId",
                        column: x => x.BackendInstanceId,
                        principalTable: "BackendInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "BackendCampaigns",
                columns: table => new
                {
                    BackendInstanceId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    SyncedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey(
                        "PK_BackendCampaigns",
                        x => new { x.BackendInstanceId, x.CampaignId }
                    );
                    table.ForeignKey(
                        name: "FK_BackendCampaigns_BackendInstances_BackendInstanceId",
                        column: x => x.BackendInstanceId,
                        principalTable: "BackendInstances",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_BackendCampaigns_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignAssets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    AssetId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Position = table.Column<int>(type: "INTEGER", nullable: false),
                    DurationSeconds = table.Column<int>(type: "INTEGER", nullable: true),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAssets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAssets_Assets_AssetId",
                        column: x => x.AssetId,
                        principalTable: "Assets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CampaignAssets_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateTable(
                name: "CampaignAssignment",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    CampaignId = table.Column<Guid>(type: "TEXT", nullable: false),
                    DisplayId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "TEXT", nullable: false),
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CampaignAssignment", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CampaignAssignment_Campaigns_CampaignId",
                        column: x => x.CampaignId,
                        principalTable: "Campaigns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                    table.ForeignKey(
                        name: "FK_CampaignAssignment_Display_DisplayId",
                        column: x => x.DisplayId,
                        principalTable: "Display",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade
                    );
                }
            );

            migrationBuilder.CreateIndex(name: "IX_Assets_Type", table: "Assets", column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_BackendAssets_AssetId",
                table: "BackendAssets",
                column: "AssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BackendCampaigns_CampaignId",
                table: "BackendCampaigns",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_BackendInstances_BaseUrl",
                table: "BackendInstances",
                column: "BaseUrl",
                unique: true
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_AssetId",
                table: "CampaignAssets",
                column: "AssetId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId",
                table: "CampaignAssets",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssets_CampaignId_Position",
                table: "CampaignAssets",
                columns: new[] { "CampaignId", "Position" }
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignment_CampaignId",
                table: "CampaignAssignment",
                column: "CampaignId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_CampaignAssignment_DisplayId",
                table: "CampaignAssignment",
                column: "DisplayId"
            );

            migrationBuilder.CreateIndex(
                name: "IX_Campaigns_Name",
                table: "Campaigns",
                column: "Name"
            );

            migrationBuilder.CreateIndex(
                name: "IX_DownloadedAssets_IsDownloaded",
                table: "DownloadedAssets",
                column: "IsDownloaded"
            );
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BackendAssets");

            migrationBuilder.DropTable(name: "BackendCampaigns");

            migrationBuilder.DropTable(name: "BackendCredentials");

            migrationBuilder.DropTable(name: "CampaignAssets");

            migrationBuilder.DropTable(name: "CampaignAssignment");

            migrationBuilder.DropTable(name: "DownloadedAssets");

            migrationBuilder.DropTable(name: "Assets");

            migrationBuilder.DropTable(name: "Campaigns");

            migrationBuilder.DropTable(name: "Display");

            migrationBuilder.DropTable(name: "BackendInstances");
        }
    }
}
