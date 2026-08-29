using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerPower : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerPowers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    ArmyPower = table.Column<double>(type: "double precision", nullable: false),
                    HeroPower = table.Column<double>(type: "double precision", nullable: false),
                    EquipmentPower = table.Column<double>(type: "double precision", nullable: false),
                    TotalPower = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerPowers", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPowers_PlayerId",
                table: "PlayerPowers",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerPowers_ServerId_TotalPower",
                table: "PlayerPowers",
                columns: new[] { "ServerId", "TotalPower" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerPowers");
        }
    }
}
