using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuestServerIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "QuestProgress",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "QuestProgress" q SET "ServerId" = p."ServerId"
                FROM "Players" p WHERE p."Id" = q."PlayerId";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgress_ServerId",
                table: "QuestProgress",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestProgress_ServerId",
                table: "QuestProgress");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "QuestProgress");
        }
    }
}
