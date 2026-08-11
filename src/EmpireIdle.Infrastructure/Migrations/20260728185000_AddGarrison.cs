using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGarrison : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Garrisons",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    VillageId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Garrisons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UnitTrainingOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false),
                    CompletesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UnitTrainingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_UnitTrainingOrders_Garrisons_GarrisonId",
                        column: x => x.GarrisonId,
                        principalTable: "Garrisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VillageUnits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Count = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VillageUnits_Garrisons_GarrisonId",
                        column: x => x.GarrisonId,
                        principalTable: "Garrisons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Garrisons_VillageId",
                table: "Garrisons",
                column: "VillageId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTrainingOrders_CompletesAt",
                table: "UnitTrainingOrders",
                column: "CompletesAt");

            migrationBuilder.CreateIndex(
                name: "IX_UnitTrainingOrders_GarrisonId",
                table: "UnitTrainingOrders",
                column: "GarrisonId");

            migrationBuilder.CreateIndex(
                name: "IX_VillageUnits_GarrisonId_UnitType",
                table: "VillageUnits",
                columns: new[] { "GarrisonId", "UnitType" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UnitTrainingOrders");

            migrationBuilder.DropTable(
                name: "VillageUnits");

            migrationBuilder.DropTable(
                name: "Garrisons");
        }
    }
}
