using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Право обирати кланову технологію (128) у стандартних ролях наявних кланів — тих самих, що й у нового клану.
    /// Власні ролі не чіпаємо: їхні дозволи визначив лідер.
    /// </summary>
    public partial class GrantManageTechnologies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                UPDATE "ClanRoles" SET "Permissions" = "Permissions" | 128
                WHERE "Key" IN ('leader', 'deputy', 'general', 'officer');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""UPDATE "ClanRoles" SET "Permissions" = "Permissions" & ~128;""");
        }
    }
}
