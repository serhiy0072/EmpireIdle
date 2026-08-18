using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class QuestSkeleton : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "QuestProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClaimedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerQuestContributions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    QuestKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    PlayerId = table.Column<Guid>(type: "uuid", nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    LastContributedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerQuestContributions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ServerQuestProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    QuestKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Total = table.Column<long>(type: "bigint", nullable: false),
                    Target = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerQuestProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "QuestObjectiveProgress",
                columns: table => new
                {
                    QuestProgressId = table.Column<Guid>(type: "uuid", nullable: false),
                    Index = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<int>(type: "integer", nullable: false),
                    Required = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_QuestObjectiveProgress", x => new { x.QuestProgressId, x.Index });
                    table.ForeignKey(
                        name: "FK_QuestObjectiveProgress_QuestProgress_QuestProgressId",
                        column: x => x.QuestProgressId,
                        principalTable: "QuestProgress",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgress_PlayerId_QuestKey",
                table: "QuestProgress",
                columns: new[] { "PlayerId", "QuestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerQuestContributions_ServerId_QuestKey_Amount",
                table: "ServerQuestContributions",
                columns: new[] { "ServerId", "QuestKey", "Amount" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "IX_ServerQuestContributions_ServerId_QuestKey_PlayerId",
                table: "ServerQuestContributions",
                columns: new[] { "ServerId", "QuestKey", "PlayerId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerQuestProgress_ServerId_QuestKey",
                table: "ServerQuestProgress",
                columns: new[] { "ServerId", "QuestKey" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "QuestObjectiveProgress");

            migrationBuilder.DropTable(
                name: "ServerQuestContributions");

            migrationBuilder.DropTable(
                name: "ServerQuestProgress");

            migrationBuilder.DropTable(
                name: "QuestProgress");
        }
    }
}
