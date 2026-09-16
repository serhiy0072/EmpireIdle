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
            // Таблиця вже в множині: перейменування зробила попередня міграція.
            // Тут лишається привести до множини первинний і зовнішній ключі —
            // Postgres їх при RenameTable не чіпає
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRolls");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRolls");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentRolls_EquipmentItemId",
                table: "EquipmentRolls");

            // Обидва покриваються префіксами складених індексів
            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_EquippedByHeroId",
                table: "EquipmentItems");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_PlayerId",
                table: "EquipmentItems");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRolls",
                table: "EquipmentRolls",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRolls_EquipmentItemId_Level",
                table: "EquipmentRolls",
                columns: new[] { "EquipmentItemId", "Level" });

            migrationBuilder.AddForeignKey(
                name: "FK_EquipmentRolls_EquipmentItems_EquipmentItemId",
                table: "EquipmentRolls",
                column: "EquipmentItemId",
                principalTable: "EquipmentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentRolls_EquipmentItems_EquipmentItemId",
                table: "EquipmentRolls");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EquipmentRolls",
                table: "EquipmentRolls");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentRolls_EquipmentItemId_Level",
                table: "EquipmentRolls");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRolls",
                column: "Id");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentRolls_EquipmentItemId",
                table: "EquipmentRolls",
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
                table: "EquipmentRolls",
                column: "EquipmentItemId",
                principalTable: "EquipmentItems",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
