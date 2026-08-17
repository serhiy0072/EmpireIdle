using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServerIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "OutboxMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Наявні повідомлення створювались до мультисервера — прив'язуємо до першого світу
            migrationBuilder.Sql("""
                UPDATE "OutboxMessages" SET "ServerId" = 1 WHERE "ServerId" = 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "OutboxMessages");
        }
    }
}
