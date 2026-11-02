using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClanRoleKey : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Key",
                table: "ClanRoles",
                type: "character varying(32)",
                maxLength: 32,
                nullable: true);

            // Наявні клани: ключ отримує лише роль зі стандартною назвою — перейменовані лишаються назвою клану
            migrationBuilder.Sql("""
                UPDATE "ClanRoles" SET "Key" = lower("Name")
                WHERE "Name" IN ('Leader', 'Deputy', 'General', 'Officer', 'Veteran', 'Member');
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Key",
                table: "ClanRoles");
        }
    }
}
