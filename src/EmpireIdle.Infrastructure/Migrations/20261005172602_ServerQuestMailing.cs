using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ServerQuestMailing : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "MailedThroughPlayerId",
                table: "ServerQuestProgress",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RewardsMailedAt",
                table: "ServerQuestProgress",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MailedThroughPlayerId",
                table: "ServerQuestProgress");

            migrationBuilder.DropColumn(
                name: "RewardsMailedAt",
                table: "ServerQuestProgress");
        }
    }
}
