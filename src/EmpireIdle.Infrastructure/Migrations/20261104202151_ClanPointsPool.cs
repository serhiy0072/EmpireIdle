using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClanPointsPool : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TargetKey",
                table: "ClanResearch");

            migrationBuilder.AddColumn<long>(
                name: "UnallocatedPoints",
                table: "ClanResearch",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UnallocatedPoints",
                table: "ClanResearch");

            migrationBuilder.AddColumn<string>(
                name: "TargetKey",
                table: "ClanResearch",
                type: "character varying(80)",
                maxLength: 80,
                nullable: true);
        }
    }
}
