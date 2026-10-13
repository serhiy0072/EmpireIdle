using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BuildingPlanPosition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "PlanX",
                table: "Buildings",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "PlanY",
                table: "Buildings",
                type: "double precision",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PlanX",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "PlanY",
                table: "Buildings");
        }
    }
}
