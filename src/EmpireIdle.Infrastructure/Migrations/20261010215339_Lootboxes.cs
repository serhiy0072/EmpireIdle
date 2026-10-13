using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Lootboxes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LootboxPity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LootboxKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Since = table.Column<int>(type: "integer", nullable: false),
                    TotalOpened = table.Column<int>(type: "integer", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LootboxPity", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LootboxPity_PlayerId_LootboxKey",
                table: "LootboxPity",
                columns: new[] { "PlayerId", "LootboxKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LootboxPity");
        }
    }
}
