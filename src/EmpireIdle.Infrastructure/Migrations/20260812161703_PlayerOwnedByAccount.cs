using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerOwnedByAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "Players",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            // Зв'язок акаунт↔гравець існує лише через email — відновлюємо його до створення індексу
            migrationBuilder.Sql("""
                UPDATE "Players" p SET "UserId" = u."Id"
                FROM "AspNetUsers" u WHERE u."NormalizedEmail" = UPPER(p."Email");
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Players_UserId",
                table: "Players",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Players_UserId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "Players");
        }
    }
}
