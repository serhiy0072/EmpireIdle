using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeastPens : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TamedBeastKey",
                table: "BattleReports",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BeastPens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeastPens", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Beasts",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BeastPenId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeastKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    TamedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beasts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beasts_BeastPens_BeastPenId",
                        column: x => x.BeastPenId,
                        principalTable: "BeastPens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "BeastTamingPity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BeastPenId = table.Column<Guid>(type: "uuid", nullable: false),
                    BeastKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Misses = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BeastTamingPity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BeastTamingPity_BeastPens_BeastPenId",
                        column: x => x.BeastPenId,
                        principalTable: "BeastPens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BeastPens_PlayerId",
                table: "BeastPens",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Beasts_BeastPenId_BeastKey",
                table: "Beasts",
                columns: new[] { "BeastPenId", "BeastKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BeastTamingPity_BeastPenId_BeastKey",
                table: "BeastTamingPity",
                columns: new[] { "BeastPenId", "BeastKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Beasts");

            migrationBuilder.DropTable(
                name: "BeastTamingPity");

            migrationBuilder.DropTable(
                name: "BeastPens");

            migrationBuilder.DropColumn(
                name: "TamedBeastKey",
                table: "BattleReports");
        }
    }
}
