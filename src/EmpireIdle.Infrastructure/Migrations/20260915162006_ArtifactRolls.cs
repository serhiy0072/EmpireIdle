using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ArtifactRolls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EquipmentRoll",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    EquipmentItemId = table.Column<Guid>(type: "uuid", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Seed = table.Column<int>(type: "integer", nullable: false),
                    RolledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EquipmentRoll", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                        column: x => x.EquipmentItemId,
                        principalTable: "EquipmentItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRoll_EquipmentItemId", 
                table: "EquipmentRoll",
                column: "EquipmentItemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EquipmentRoll");
        }
    }
}
