using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AggregateUpdatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "QuestProgress",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "PlayerWallets",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Marches",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Garrisons",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.Sql("""
                UPDATE "Garrisons" SET "UpdatedAt" = CURRENT_TIMESTAMP;
                UPDATE "Villages" SET "UpdatedAt" = CURRENT_TIMESTAMP;
                UPDATE "Marches" SET "UpdatedAt" = CURRENT_TIMESTAMP;
                UPDATE "QuestProgress" SET "UpdatedAt" = CURRENT_TIMESTAMP;
                UPDATE "PlayerWallets" SET "UpdatedAt" = CURRENT_TIMESTAMP;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "QuestProgress");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "PlayerWallets");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Marches");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Garrisons");
        }
    }
}
