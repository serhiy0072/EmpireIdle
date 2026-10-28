using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddVipPurchasedGems : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "PurchasedGems",
                table: "PlayerWallets",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            // VIP рахується від усіх уже зроблених покупок, а не з моменту міграції
            migrationBuilder.Sql("""
                UPDATE "PlayerWallets" AS w
                SET "PurchasedGems" = COALESCE(
                    (SELECT SUM(t."Amount") FROM "WalletTransactions" AS t WHERE t."WalletId" = w."Id" AND t."Type" = 0), 0);
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PurchasedGems",
                table: "PlayerWallets");
        }
    }
}
