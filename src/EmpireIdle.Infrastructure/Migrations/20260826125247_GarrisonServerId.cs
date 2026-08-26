using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class GarrisonServerId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ServerId",
                table: "Garrisons",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // Світ гарнізону — це світ його села
            migrationBuilder.Sql("""
                UPDATE "Garrisons" g SET "ServerId" = v."ServerId"
                FROM "Villages" v WHERE v."Id" = g."VillageId";
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ServerId",
                table: "Garrisons");
        }
    }
}
