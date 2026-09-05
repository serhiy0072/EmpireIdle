using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Reinforcements : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ReinforcementUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerGarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReinforcementUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReinforcementUnits_Garrisons_GarrisonId",
                        column: x => x.GarrisonId,
                        principalTable: "Garrisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReinforcementUnits_GarrisonId_OwnerPlayerId_UnitType",
                table: "ReinforcementUnits",
                columns: new[] { "GarrisonId", "OwnerPlayerId", "UnitType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReinforcementUnits_OwnerPlayerId",
                table: "ReinforcementUnits",
                column: "OwnerPlayerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReinforcementUnits");
        }
    }
}
