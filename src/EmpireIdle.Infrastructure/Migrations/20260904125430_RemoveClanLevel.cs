using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveClanLevel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Level",
                table: "Clans");

            migrationBuilder.CreateTable(
                name: "ClanHelpRequests",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    TargetType = table.Column<int>(type: "integer", nullable: false),
                    TargetId = table.Column<Guid>(type: "uuid", nullable: false),
                    FullDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanHelpRequests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClanHelpContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: false),
                    HelperId = table.Column<Guid>(type: "uuid", nullable: false),
                    HelpedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanHelpContributions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClanHelpContributions_ClanHelpRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "ClanHelpRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ClanHelpContributions_RequestId_HelperId",
                table: "ClanHelpContributions",
                columns: new[] { "RequestId", "HelperId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClanHelpRequests_ClanId_ExpiresAt",
                table: "ClanHelpRequests",
                columns: new[] { "ClanId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ClanHelpRequests_TargetId",
                table: "ClanHelpRequests",
                column: "TargetId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanHelpContributions");

            migrationBuilder.DropTable(
                name: "ClanHelpRequests");

            migrationBuilder.AddColumn<int>(
                name: "Level",
                table: "Clans",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }
    }
}
