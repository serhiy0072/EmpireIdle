using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MarchDepartedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "DepartedAt",
                table: "Marches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Наявним маршам виходу не знали — беремо час прибуття як наближення.
            // Точність не важлива: розворот після переїзду стосується лише нових.
            migrationBuilder.Sql("""UPDATE "Marches" SET "DepartedAt" = "ArrivesAt";""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DepartedAt",
                table: "Marches");
        }
    }
}
