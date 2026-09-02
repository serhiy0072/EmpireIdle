using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class PlayerRating : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerRatings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    MonstersDefeated = table.Column<int>(type: "integer", nullable: false),
                    BattlesWon = table.Column<int>(type: "integer", nullable: false),
                    QuestsCompleted = table.Column<int>(type: "integer", nullable: false),
                    ServerContribution = table.Column<int>(type: "integer", nullable: false),
                    PowerScore = table.Column<double>(type: "double precision", nullable: false),
                    DevelopmentScore = table.Column<double>(type: "double precision", nullable: false),
                    ActivityScore = table.Column<double>(type: "double precision", nullable: false),
                    TotalRating = table.Column<int>(type: "integer", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerRatings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_PlayerId",
                table: "PlayerRatings",
                column: "PlayerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PlayerRatings_ServerId_TotalRating",
                table: "PlayerRatings",
                columns: new[] { "ServerId", "TotalRating" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerRatings");
        }
    }
}
