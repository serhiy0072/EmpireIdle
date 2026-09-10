using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HeroesAndRefactorEquipmentItemRarityToEnum : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // PostgreSQL не кастить varchar у integer неявно, тож згенерований
            // AlterColumn замінено явним USING. Заразом тут лишається зафіксованим
            // ретирування "legendary": воно лягає в ту саму трійку, що й "unique".
            migrationBuilder.Sql("""
                ALTER TABLE "EquipmentItems"
                ALTER COLUMN "Rarity" TYPE integer
                USING CASE lower("Rarity")
                    WHEN 'common'    THEN 1
                    WHEN 'rare'      THEN 2
                    WHEN 'unique'    THEN 3
                    WHEN 'legendary' THEN 3
                    ELSE 1
                END;
                """);

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

            migrationBuilder.Sql("""
                ALTER TABLE "EquipmentItems"
                ALTER COLUMN "Rarity" TYPE character varying(20)
                USING CASE "Rarity"
                    WHEN 1 THEN 'common'
                    WHEN 2 THEN 'rare'
                    WHEN 3 THEN 'unique'
                    ELSE 'common'
                END;
                """);
        }
    }
}
