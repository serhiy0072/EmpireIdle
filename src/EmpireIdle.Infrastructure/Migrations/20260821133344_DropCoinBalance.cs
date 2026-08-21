using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropCoinBalance : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_UnitTrainingOrders_CompletesAt",
                table: "UnitTrainingOrders");

            migrationBuilder.DropColumn(
                name: "CoinBalance",
                table: "PlayerWallets");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CoinBalance",
                table: "PlayerWallets",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_UnitTrainingOrders_CompletesAt",
                table: "UnitTrainingOrders",
                column: "CompletesAt");
        }
    }
}
