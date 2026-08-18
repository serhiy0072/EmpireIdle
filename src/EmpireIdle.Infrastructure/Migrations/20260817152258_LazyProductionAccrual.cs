using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class LazyProductionAccrual : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "StoredAmount",
                table: "Buildings",
                newName: "AccruedAmount");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastAccruedAt",
                table: "Buildings",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<DateTime>(
                name: "StartedAt",
                table: "ActiveEffects",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Мітки не мають лишитись у 0001 році — інакше StoredAt порахує
            // виробіток за дві тисячі років і впреться в кап
            migrationBuilder.Sql("""
                UPDATE "Buildings" SET "LastAccruedAt" = "LastCollectedAt";
                UPDATE "ActiveEffects" SET "StartedAt" = "ExpiresAt" - INTERVAL '1 hour';
                """);

            migrationBuilder.DropColumn(
                name: "LastTickAt",
                table: "Villages");

            migrationBuilder.DropColumn(
                name: "ProductionRemainder",
                table: "Buildings");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastAccruedAt",
                table: "Buildings");

            migrationBuilder.DropColumn(
                name: "StartedAt",
                table: "ActiveEffects");

            migrationBuilder.RenameColumn(
                name: "AccruedAmount",
                table: "Buildings",
                newName: "StoredAmount");

            migrationBuilder.AddColumn<DateTime>(
                name: "LastTickAt",
                table: "Villages",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<double>(
                name: "ProductionRemainder",
                table: "Buildings",
                type: "double precision",
                nullable: false,
                defaultValue: 0.0);
        }
    }
}
