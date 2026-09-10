using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EquipmentItemRarityToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "Rarity",
                table: "EquipmentItems",
                type: "integer",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(20)",
                oldMaxLength: 20);

            migrationBuilder.CreateTable(
                name: "Heroes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    HeroKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Tier = table.Column<int>(type: "integer", nullable: false),
                    Level = table.Column<int>(type: "integer", nullable: false),
                    Constellation = table.Column<int>(type: "integer", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    HealedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AcquiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Heroes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "HeroLevelOrders",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    HeroId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    TargetLevel = table.Column<int>(type: "integer", nullable: false),
                    CompletesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HeroLevelOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HeroLevelOrders_Heroes_HeroId",
                        column: x => x.HeroId,
                        principalTable: "Heroes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_PlayerId_HeroKey",
                table: "Heroes",
                columns: new[] { "PlayerId", "HeroKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_PlayerId_ServerId",
                table: "Heroes",
                columns: new[] { "PlayerId", "ServerId" });

            migrationBuilder.CreateIndex(
                name: "IX_HeroLevelOrders_HeroId",
                table: "HeroLevelOrders",
                column: "HeroId");

            migrationBuilder.CreateIndex(
                name: "IX_HeroLevelOrders_PlayerId",
                table: "HeroLevelOrders",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HeroLevelOrders_ServerId_CompletesAt",
                table: "HeroLevelOrders",
                columns: new[] { "ServerId", "CompletesAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "HeroLevelOrders");

            migrationBuilder.DropTable(
                name: "Heroes");

            migrationBuilder.AlterColumn<string>(
                name: "Rarity",
                table: "EquipmentItems",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(int),
                oldType: "integer");
        }
    }
}
