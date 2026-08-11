using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBattleReports : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BattleReports",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    MarchId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    TerrainType = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    TargetName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    TargetLevel = table.Column<int>(type: "integer", nullable: false),
                    Won = table.Column<bool>(type: "boolean", nullable: false),
                    AttackerPower = table.Column<double>(type: "double precision", nullable: false),
                    DefenderPower = table.Column<double>(type: "double precision", nullable: false),
                    FoughtAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IsRead = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleReports", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "BattleReportLines",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    BattleReportId = table.Column<Guid>(type: "uuid", nullable: false),
                    UnitType = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Sent = table.Column<int>(type: "integer", nullable: false),
                    Wounded = table.Column<int>(type: "integer", nullable: false),
                    Instant = table.Column<int>(type: "integer", nullable: false),
                    Dead = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BattleReportLines", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BattleReportLines_BattleReports_BattleReportId",
                        column: x => x.BattleReportId,
                        principalTable: "BattleReports",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BattleReportLines_BattleReportId",
                table: "BattleReportLines",
                column: "BattleReportId");

            migrationBuilder.CreateIndex(
                name: "IX_BattleReports_PlayerId_FoughtAt",
                table: "BattleReports",
                columns: new[] { "PlayerId", "FoughtAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BattleReportLines");

            migrationBuilder.DropTable(
                name: "BattleReports");
        }
    }
}
