using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameEquipmentRollsTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // ArtifactRolls створила таблицю в однині, EquipmentIndexes її не перейменувала:
            // модель пише в EquipmentRolls, а база має EquipmentRoll
            migrationBuilder.DropForeignKey(
                name: "FK_EquipmentRoll_EquipmentItems_EquipmentItemId",
                table: "EquipmentRoll");

            migrationBuilder.DropPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll");

            migrationBuilder.RenameTable(
                name: "EquipmentRoll",
                newName: "EquipmentRolls");

            migrationBuilder.RenameIndex(
                name: "IX_EquipmentRoll_EquipmentItemId_Level",
                table: "EquipmentRolls",
                newName: "IX_EquipmentRolls_EquipmentItemId_Level");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRolls",
                table: "EquipmentRolls",
                column: "Id");

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

            migrationBuilder.RenameTable(
                name: "EquipmentRolls",
                newName: "EquipmentRoll");

            migrationBuilder.RenameIndex(
                name: "IX_EquipmentRolls_EquipmentItemId_Level",
                table: "EquipmentRoll",
                newName: "IX_EquipmentRoll_EquipmentItemId_Level");

            migrationBuilder.AddPrimaryKey(
                name: "PK_EquipmentRoll",
                table: "EquipmentRoll",
                column: "Id");

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
