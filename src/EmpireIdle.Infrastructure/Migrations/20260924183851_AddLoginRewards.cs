using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddLoginRewards : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<Guid>(
                name: "ReferenceId",
                table: "MailLetters",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<DateTime>(
                name: "ClaimedAt",
                table: "MailLetters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Rewards",
                table: "MailLetters",
                type: "jsonb",
                nullable: false,
                defaultValue: "[]");

            migrationBuilder.AddColumn<int>(
                name: "Sequence",
                table: "MailLetters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<uint>(
                name: "xmin",
                table: "MailLetters",
                type: "xid",
                rowVersion: true,
                nullable: false,
                defaultValue: 0u);

            migrationBuilder.CreateTable(
                name: "LoginRewardProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    LastCheckIn = table.Column<DateOnly>(type: "date", nullable: true),
                    Streak = table.Column<int>(type: "integer", nullable: false),
                    RewardedWeek = table.Column<DateOnly>(type: "date", nullable: true),
                    RewardedMonth = table.Column<DateOnly>(type: "date", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LoginRewardProgress", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LoginRewardProgress_PlayerId",
                table: "LoginRewardProgress",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LoginRewardProgress");

            migrationBuilder.DropColumn(
                name: "ClaimedAt",
                table: "MailLetters");

            migrationBuilder.DropColumn(
                name: "Rewards",
                table: "MailLetters");

            migrationBuilder.DropColumn(
                name: "Sequence",
                table: "MailLetters");

            migrationBuilder.DropColumn(
                name: "xmin",
                table: "MailLetters");

            migrationBuilder.AlterColumn<Guid>(
                name: "ReferenceId",
                table: "MailLetters",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);
        }
    }
}
