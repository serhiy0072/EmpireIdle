using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class ReviewIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestProgress_ServerId",
                table: "QuestProgress");

            migrationBuilder.DropIndex(
                name: "IX_Players_Email",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Marches_State_ArrivesAt",
                table: "Marches");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgress_ServerId_StartedAt",
                table: "QuestProgress",
                columns: new[] { "ServerId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Players_ClanId",
                table: "Players",
                column: "ClanId",
                filter: "\"ClanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Marches_ServerId_ArrivesAt",
                table: "Marches",
                columns: new[] { "ServerId", "ArrivesAt" },
                filter: "\"State\" <> 3 AND \"State\" <> 4");

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveEffects_ExpiresAt",
                table: "ActiveEffects",
                column: "ExpiresAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_QuestProgress_ServerId_StartedAt",
                table: "QuestProgress");

            migrationBuilder.DropIndex(
                name: "IX_Players_ClanId",
                table: "Players");

            migrationBuilder.DropIndex(
                name: "IX_Marches_ServerId_ArrivesAt",
                table: "Marches");

            migrationBuilder.DropIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords");

            migrationBuilder.DropIndex(
                name: "IX_ActiveEffects_ExpiresAt",
                table: "ActiveEffects");

            migrationBuilder.CreateIndex(
                name: "IX_QuestProgress_ServerId",
                table: "QuestProgress",
                column: "ServerId");

            migrationBuilder.CreateIndex(
                name: "IX_Players_Email",
                table: "Players",
                column: "Email");

            migrationBuilder.CreateIndex(
                name: "IX_Marches_State_ArrivesAt",
                table: "Marches",
                columns: new[] { "State", "ArrivesAt" });

            migrationBuilder.CreateIndex(
                name: "IX_IdempotencyRecords_CreatedAt",
                table: "IdempotencyRecords",
                column: "CreatedAt",
                filter: "\"ResponseJson\" IS NULL");
        }
    }
}
