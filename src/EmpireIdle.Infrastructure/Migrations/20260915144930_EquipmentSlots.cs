using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentSlots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBroken",
                table: "EquipmentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "EquipmentItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "SlotIndex",
                table: "EquipmentItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "EquipmentItems",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "EquipmentItems",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_EquippedByHeroId_Slot_SlotIndex",
                table: "EquipmentItems",
                columns: new[] { "EquippedByHeroId", "Slot", "SlotIndex" },
                unique: true,
                filter: "\"EquippedByHeroId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_EquipmentItems_PlayerId_ServerId",
                table: "EquipmentItems",
                columns: new[] { "PlayerId", "ServerId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_EquippedByHeroId_Slot_SlotIndex",
                table: "EquipmentItems");

            migrationBuilder.DropIndex(
                name: "IX_EquipmentItems_PlayerId_ServerId",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "IsBroken",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "SlotIndex",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "EquipmentItems");
        }
    }
}
