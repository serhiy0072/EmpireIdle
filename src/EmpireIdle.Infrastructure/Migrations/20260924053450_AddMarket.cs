using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarket : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ResaleLockedUntil",
                table: "Heroes",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnMarket",
                table: "EquipmentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResaleLockedUntil",
                table: "EquipmentItems",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "MarketListings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    SellerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    EquipmentId = table.Column<Guid>(type: "uuid", nullable: true),
                    HeroId = table.Column<Guid>(type: "uuid", nullable: true),
                    ItemKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    Units = table.Column<double>(type: "double precision", nullable: false),
                    PricingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    PriceGold = table.Column<int>(type: "integer", nullable: false),
                    TaxGold = table.Column<int>(type: "integer", nullable: false),
                    ListedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    BuyerId = table.Column<Guid>(type: "uuid", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketListings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MarketPriceSnapshots",
                columns: table => new
                {
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    PricingKey = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    MedianPerUnit = table.Column<double>(type: "double precision", nullable: true),
                    Sales = table.Column<int>(type: "integer", nullable: false),
                    ComputedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarketPriceSnapshots", x => new { x.ServerId, x.PricingKey });
                });

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_EquipmentId",
                table: "MarketListings",
                column: "EquipmentId",
                unique: true,
                filter: "\"EquipmentId\" IS NOT NULL AND \"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_ExpiresAt",
                table: "MarketListings",
                column: "ExpiresAt",
                filter: "\"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_HeroId",
                table: "MarketListings",
                column: "HeroId",
                unique: true,
                filter: "\"HeroId\" IS NOT NULL AND \"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_ServerId_Kind_ItemKey",
                table: "MarketListings",
                columns: new[] { "ServerId", "Kind", "ItemKey" },
                filter: "\"State\" = 1");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_ServerId_PricingKey_ClosedAt",
                table: "MarketListings",
                columns: new[] { "ServerId", "PricingKey", "ClosedAt" },
                filter: "\"State\" = 2");

            migrationBuilder.CreateIndex(
                name: "IX_MarketListings_ServerId_SellerId_State",
                table: "MarketListings",
                columns: new[] { "ServerId", "SellerId", "State" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MarketListings");

            migrationBuilder.DropTable(
                name: "MarketPriceSnapshots");

            migrationBuilder.DropColumn(
                name: "ResaleLockedUntil",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "IsOnMarket",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "ResaleLockedUntil",
                table: "EquipmentItems");
        }
    }
}
