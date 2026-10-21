using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTowerProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TowerProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    HighestFloor = table.Column<int>(type: "integer", nullable: false),
                    ReachedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TowerProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TowerProgress_PlayerId",
                table: "TowerProgress",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TowerProgress_ServerId_HighestFloor_ReachedAt",
                table: "TowerProgress",
                columns: new[] { "ServerId", "HighestFloor", "ReachedAt" },
                descending: new[] { false, true, false });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TowerProgress");
        }
    }
}
