using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PaymentServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "Payments" p SET "ServerId" = pl."ServerId"
                FROM "Players" pl WHERE pl."Id" = p."PlayerId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ServerId",
                table: "Payments",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Payments_ServerId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Payments");
        }
    }
}
