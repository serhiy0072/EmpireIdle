using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BannerPityAndRollJournal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LootBoxProgress");

            migrationBuilder.CreateTable(
                name: "BannerPity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    PityGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    RareSince = table.Column<int>(type: "integer", nullable: false),
                    UniqueSince = table.Column<int>(type: "integer", nullable: false),
                    FeaturedGuaranteed = table.Column<bool>(type: "boolean", nullable: false),
                    TotalRolls = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerPity", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BannerRolls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    BannerKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PityGroup = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DropKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rarity = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    RareSinceBefore = table.Column<int>(type: "integer", nullable: false),
                    UniqueSinceBefore = table.Column<int>(type: "integer", nullable: false),
                    FeaturedGuaranteedBefore = table.Column<bool>(type: "boolean", nullable: false),
                    WasPity = table.Column<bool>(type: "boolean", nullable: false),
                    LostFiftyFifty = table.Column<bool>(type: "boolean", nullable: false),
                    PriceGems = table.Column<int>(type: "integer", nullable: false),
                    RolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BannerRolls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BannerPity_PlayerId_PityGroup",
                table: "BannerPity",
                columns: new[] { "PlayerId", "PityGroup" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BannerRolls_PlayerId_RolledAt",
                table: "BannerRolls",
                columns: new[] { "PlayerId", "RolledAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BannerPity");

            migrationBuilder.DropTable(
                name: "BannerRolls");

            migrationBuilder.CreateTable(
                name: "LootBoxProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BoxKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    SinceLastLegendary = table.Column<int>(type: "integer", nullable: false),
                    TotalOpened = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootBoxProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LootBoxProgress_PlayerId_BoxKey",
                table: "LootBoxProgress",
                columns: new[] { "PlayerId", "BoxKey" },
                unique: true);
        }
    }
}
