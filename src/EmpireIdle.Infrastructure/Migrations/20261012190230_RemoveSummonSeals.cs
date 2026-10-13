using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveSummonSeals : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Печатки призову прибрано (12.10.2026): їхні рядки журналу (типи 2 і 3) більше нічого не означають
            migrationBuilder.Sql(@"DELETE FROM ""WalletTransactions"" WHERE ""Type"" IN (2, 3);");

            migrationBuilder.DropColumn(
                name: "SealBalance",
                table: "PlayerWallets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "SealBalance",
                table: "PlayerWallets",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
