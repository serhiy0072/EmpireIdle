using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRoll");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentRoll_EquipmentItemId",
                table: "EquipmentRoll");

            // Обидва покриваються префіксами складених індексів
            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_EquippedByHeroId",
                table: "EquipmentItems");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_PlayerId",
                table: "EquipmentItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRoll_EquipmentItemId_Level",
                table: "EquipmentRoll",
                columns: new[] { "EquipmentItemId", "Level" });

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRoll",
                column: "EquipmentItemId",
                principalTable: "EquipmentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRoll");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentRoll_EquipmentItemId_Level",
                table: "EquipmentRoll");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRoll_EquipmentItemId",
                table: "EquipmentRoll",
                column: "EquipmentItemId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_EquippedByHeroId",
                table: "EquipmentItems",
                column: "EquippedByHeroId");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_PlayerId",
                table: "EquipmentItems",
                column: "PlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRoll",
                column: "EquipmentItemId",
                principalTable: "EquipmentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
