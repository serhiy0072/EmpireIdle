using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Виправляє HeroStars: та множила сузір'я 0–6 на 6, а зірок лише 5 — повне сузір'я дало 36 частинок
    /// і шосту зірку. Стеля — 5 зірок × 6 частинок (HeroesConfig.MaxStarParts на 08.10.2026).
    /// Назад не повертаємо: 31–36 частинок ніколи не були дозволеним станом.
    /// </summary>
    public partial class ClampHeroStarParts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"UPDATE ""Heroes"" SET ""StarParts"" = 30 WHERE ""StarParts"" > 30;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
