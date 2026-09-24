using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddCityFall : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ShieldUntil",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "VillageFalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackerVillageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    FromX = table.Column<int>(type: "integer", nullable: false),
                    FromY = table.Column<int>(type: "integer", nullable: false),
                    ToX = table.Column<int>(type: "integer", nullable: false),
                    ToY = table.Column<int>(type: "integer", nullable: false),
                    ShieldUntil = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VillageFalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_VillageFalls_AttackerPlayerId_OccurredAt",
                table: "VillageFalls",
                columns: new[] { "AttackerPlayerId", "OccurredAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VillageFalls_PlayerId_OccurredAt",
                table: "VillageFalls",
                columns: new[] { "PlayerId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VillageFalls");

            migrationBuilder.DropColumn(
                name: "ShieldUntil",
                table: "Villages");
        }
    }
}
