using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMapCells : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MapCells",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    OccupantType = table.Column<int>(type: "integer", nullable: false),
                    OccupantId = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MapCells", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MapCells_OccupantType_OccupantId",
                table: "MapCells",
                columns: new[] { "OccupantType", "OccupantId" });

            migrationBuilder.CreateIndex(
                name: "IX_MapCells_ServerId_X_Y",
                table: "MapCells",
                columns: new[] { "ServerId", "X", "Y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MapCells");
        }
    }
}
