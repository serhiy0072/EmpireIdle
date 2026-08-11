using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class WalletOwnedByAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UserId",
                table: "PlayerWallets",
                type: "character varying(450)",
                maxLength: 450,
                nullable: false,
                defaultValue: "");

            // Зв'язок акаунт↔гравець існує лише через email; відновлюємо його до втрати PlayerId
            migrationBuilder.Sql("""
                UPDATE "PlayerWallets" w
                SET "UserId" = u."Id"
                FROM "Players" p
                JOIN "AspNetUsers" u ON u."NormalizedEmail" = UPPER(p."Email")
                WHERE w."PlayerId" = p."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerWallets_UserId",
                table: "PlayerWallets",
                column: "UserId",
                unique: true);

            migrationBuilder.DropColumn(
                name: "PlayerId",
                table: "PlayerWallets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PlayerWallets_UserId",
                table: "PlayerWallets");

            migrationBuilder.DropColumn(
                name: "UserId",
                table: "PlayerWallets");

            migrationBuilder.AddColumn<Guid>(
                name: "PlayerId",
                table: "PlayerWallets",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));
        }
    }
}
