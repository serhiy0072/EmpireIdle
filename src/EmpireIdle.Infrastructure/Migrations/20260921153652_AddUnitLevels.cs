using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUnitLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WoundedUnits_GarrisonId_UnitType",
                table: "WoundedUnits");

            migrationBuilder.DropIndex(
                name: "IX_VillageUnits_GarrisonId_UnitType",
                table: "VillageUnits");

            migrationBuilder.DropIndex(
                name: "IX_ReinforcementUnits_GarrisonId_OwnerPlayerId_UnitType",
                table: "ReinforcementUnits");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "WoundedUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "VillageUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "UnitTrainingOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "ReinforcementUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "RecoverableUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "MarchUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "UnitLevelUpOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FromLevel = table.Column<int>(type: "integer", nullable: false),
                    ToLevel = table.Column<int>(type: "integer", nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CompletesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitLevelUpOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitLevelUpOrders_Garrisons_GarrisonId",
                        column: x => x.GarrisonId,
                        principalTable: "Garrisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_WoundedUnits_GarrisonId_UnitType_Level",
                table: "WoundedUnits",
                columns: new[] { "GarrisonId", "UnitType", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_GarrisonId_UnitType_Level",
                table: "VillageUnits",
                columns: new[] { "GarrisonId", "UnitType", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReinforcementUnits_GarrisonId_OwnerPlayerId_UnitType_Level",
                table: "ReinforcementUnits",
                columns: new[] { "GarrisonId", "OwnerPlayerId", "UnitType", "Level" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitLevelUpOrders_CompletesAt_GarrisonId",
                table: "UnitLevelUpOrders",
                columns: new[] { "CompletesAt", "GarrisonId" });

            migrationBuilder.CreateIndex(
                name: "IX_UnitLevelUpOrders_GarrisonId",
                table: "UnitLevelUpOrders",
                column: "GarrisonId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitLevelUpOrders");

            migrationBuilder.DropIndex(
                name: "IX_WoundedUnits_GarrisonId_UnitType_Level",
                table: "WoundedUnits");

            migrationBuilder.DropIndex(
                name: "IX_VillageUnits_GarrisonId_UnitType_Level",
                table: "VillageUnits");

            migrationBuilder.DropIndex(
                name: "IX_ReinforcementUnits_GarrisonId_OwnerPlayerId_UnitType_Level",
                table: "ReinforcementUnits");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "WoundedUnits");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "VillageUnits");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "UnitTrainingOrders");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "ReinforcementUnits");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "RecoverableUnits");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "MarchUnits");

            migrationBuilder.CreateIndex(
                name: "IX_WoundedUnits_GarrisonId_UnitType",
                table: "WoundedUnits",
                columns: new[] { "GarrisonId", "UnitType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_GarrisonId_UnitType",
                table: "VillageUnits",
                columns: new[] { "GarrisonId", "UnitType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ReinforcementUnits_GarrisonId_OwnerPlayerId_UnitType",
                table: "ReinforcementUnits",
                columns: new[] { "GarrisonId", "OwnerPlayerId", "UnitType" },
                unique: true);
        }
    }
}
