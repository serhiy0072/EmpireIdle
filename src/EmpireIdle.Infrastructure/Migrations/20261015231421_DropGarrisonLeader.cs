using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class DropGarrisonLeader : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Heroes_StationedGarrisonId_PlayerId",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "IsLeader",
                table: "Heroes");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsLeader",
                table: "Heroes",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_StationedGarrisonId_PlayerId",
                table: "Heroes",
                columns: new[] { "StationedGarrisonId", "PlayerId" },
                unique: true,
                filter: "\"IsLeader\" AND \"StationedGarrisonId\" IS NOT NULL");
        }
    }
}
