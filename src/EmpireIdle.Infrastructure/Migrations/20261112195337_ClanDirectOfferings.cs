using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClanDirectOfferings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ResourceOfferings",
                table: "ClanResearch");

            migrationBuilder.DropColumn(
                name: "UnallocatedPoints",
                table: "ClanResearch");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "ResourceOfferings",
                table: "ClanResearch",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "UnallocatedPoints",
                table: "ClanResearch",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }
    }
}
