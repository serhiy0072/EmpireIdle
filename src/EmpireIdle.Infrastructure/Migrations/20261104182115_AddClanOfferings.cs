using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClanOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ClanContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    Today = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanContributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClanOfferingLedgers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    Coins = table.Column<long>(type: "bigint", nullable: false),
                    AttemptsSpent = table.Column<int>(type: "integer", nullable: false),
                    AttemptsAnchor = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Day = table.Column<DateOnly>(type: "date", nullable: false),
                    GemOfferingsToday = table.Column<int>(type: "integer", nullable: false),
                    PointsToday = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanOfferingLedgers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClanContributions_ClanId_PlayerId",
                table: "ClanContributions",
                columns: new[] { "ClanId", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClanOfferingLedgers_PlayerId",
                table: "ClanOfferingLedgers",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanContributions");

            migrationBuilder.DropTable(
                name: "ClanOfferingLedgers");
        }
    }
}
