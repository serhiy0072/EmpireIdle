using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ClanHelpRequestConcurrency : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "ClanHelpRequests",
                type: "timestamp with time zone",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Чинні запити не змінювались після створення
            migrationBuilder.Sql("UPDATE \"ClanHelpRequests\" SET \"UpdatedAt\" = \"CreatedAt\";");

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "ClanHelpRequests",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "ClanHelpRequests");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "ClanHelpRequests");
        }
    }
}
