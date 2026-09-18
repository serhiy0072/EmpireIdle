using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentUniqueItemPerHero : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_EquippedByHeroId_ItemKey",
                table: "EquipmentItems",
                columns: new[] { "EquippedByHeroId", "ItemKey" },
                unique: true,
                filter: "\"EquippedByHeroId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_EquippedByHeroId_ItemKey",
                table: "EquipmentItems");
        }
    }
}
