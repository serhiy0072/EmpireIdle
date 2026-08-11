using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "Villages",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "Players",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.CreateIndex(
                name: "IX_Villages_ServerId",
                table: "Villages",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_ServerId",
                table: "Players",
                column: "ServerId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Villages_ServerId",
                table: "Villages");

            migrationBuilder.DropIndex(
                name: "IX_Players_ServerId",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Players");
        }
    }
}
