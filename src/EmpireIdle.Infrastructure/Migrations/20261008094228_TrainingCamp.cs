using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TrainingCamp : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampLevel",
                table: "Heroes",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CampSlot",
                table: "Heroes",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "TrainingCamps",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    PurchasedSlots = table.Column<int>(type: "integer", nullable: false),
                    SlotCooldowns = table.Column<string>(type: "jsonb", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TrainingCamps", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Heroes_PlayerId_CampSlot",
                table: "Heroes",
                columns: new[] { "PlayerId", "CampSlot" },
                unique: true,
                filter: "\"CampSlot\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_TrainingCamps_PlayerId",
                table: "TrainingCamps",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TrainingCamps");

            migrationBuilder.DropIndex(
                name: "IX_Heroes_PlayerId_CampSlot",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "CampLevel",
                table: "Heroes");

            migrationBuilder.DropColumn(
                name: "CampSlot",
                table: "Heroes");
        }
    }
}
