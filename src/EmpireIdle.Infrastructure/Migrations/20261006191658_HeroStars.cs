using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class HeroStars : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Constellation",
                table: "Heroes",
                newName: "StarParts");

            // Сузір'я n (0–6) — це n повних зірок по 6 частинок (GDD §6.1)
            migrationBuilder.Sql(@"UPDATE ""Heroes"" SET ""StarParts"" = ""StarParts"" * 6;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Heroes"" SET ""StarParts"" = ""StarParts"" / 6;");

            migrationBuilder.RenameColumn(
                name: "StarParts",
                table: "Heroes",
                newName: "Constellation");
        }
    }
}
