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
                name: "ReinforcementUnit",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    OwnerGarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "text", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    ArrivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ReinforcementUnit", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ReinforcementUnit_Garrisons_GarrisonId",
                        column: x => x.GarrisonId,
                        principalTable: "Garrisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ReinforcementUnit_GarrisonId",
                table: "ReinforcementUnit",
                column: "GarrisonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ReinforcementUnit");
        }
    }
}
