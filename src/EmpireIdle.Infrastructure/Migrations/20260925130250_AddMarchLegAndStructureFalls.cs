using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMarchLegAndStructureFalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LegStartedAt",
                table: "Marches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Наявні марші: у дорозі до цілі нога почалась із виходу; у зворотній — точного
            // моменту розвороту немає, найближче до нього — остання мутація маршу (State 2 = Returning)
            migrationBuilder.Sql("""
                UPDATE "Marches"
                SET "LegStartedAt" = CASE WHEN "State" = 2 THEN "UpdatedAt" ELSE "DepartedAt" END;
                """);

            migrationBuilder.CreateTable(
                name: "StructureFalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: false),
                    StructureId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    AttackerPlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    AttackerVillageName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    OccurredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StructureFalls", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_StructureFalls_ClanId_OccurredAt",
                table: "StructureFalls",
                columns: new[] { "ClanId", "OccurredAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "StructureFalls");

            migrationBuilder.DropColumn(
                name: "LegStartedAt",
                table: "Marches");
        }
    }
}
