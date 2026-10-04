using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBeastLevels : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "Experience",
                table: "Beasts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Звір починає з першого рівня: уже приручені отримують 1, а не 0
            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Beasts",
                type: "integer",
                nullable: false,
                defaultValue: 1);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Experience",
                table: "Beasts");

            migrationBuilder.DropColumn(
                name: "Level",
                table: "Beasts");
        }
    }
}
