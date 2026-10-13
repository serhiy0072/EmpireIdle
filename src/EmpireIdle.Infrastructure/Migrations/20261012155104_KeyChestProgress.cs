using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class KeyChestProgress : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KeyChestProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ChestKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    FreeDay = table.Column<DateOnly>(type: "date", nullable: true),
                    FreeOnDay = table.Column<int>(type: "integer", nullable: false),
                    LastFreeAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KeyChestProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_KeyChestProgress_PlayerId_ChestKey",
                table: "KeyChestProgress",
                columns: new[] { "PlayerId", "ChestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KeyChestProgress");
        }
    }
}
