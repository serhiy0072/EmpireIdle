using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RenameAcademyToLaboratory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Академію перейменовано на Лабораторію (рішення 29.10.2026) — ключ будівлі в конфігу теж
            migrationBuilder.Sql("""UPDATE "Buildings" SET "Type" = 'laboratory' WHERE "Type" = 'academy';""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "Buildings" SET "Type" = 'academy' WHERE "Type" = 'laboratory';""");
        }
    }
}
