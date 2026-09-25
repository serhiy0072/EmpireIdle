using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddClanTerritory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Garrisons_VillageId",
                table: "Garrisons");

            migrationBuilder.RenameColumn(
                name: "VillageId",
                table: "Garrisons",
                newName: "HostId");

            // Усі наявні гарнізони — сільські (GarrisonHost.Village = 1); 0 не відповідав би жодному господарю
            migrationBuilder.AddColumn<int>(
                name: "HostKind",
                table: "Garrisons",
                type: "integer",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<long>(
                name: "ContributionPoints",
                table: "Clans",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<long>(
                name: "Contribution",
                table: "ClanMembers",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.CreateTable(
                name: "ClanQuestProgress",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: false),
                    QuestKey = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Amount = table.Column<long>(type: "bigint", nullable: false),
                    Target = table.Column<long>(type: "bigint", nullable: false),
                    State = table.Column<int>(type: "integer", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanQuestProgress", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ClanStructures",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: false),
                    X = table.Column<int>(type: "integer", nullable: false),
                    Y = table.Column<int>(type: "integer", nullable: false),
                    GarrisonId = table.Column<Guid>(type: "uuid", nullable: false),
                    PlacedBy = table.Column<Guid>(type: "uuid", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    BuildDuration = table.Column<TimeSpan>(type: "interval", nullable: false),
                    CompletesAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcceleratedShare = table.Column<double>(type: "double precision", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClanStructures", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Garrisons_HostKind_HostId",
                table: "Garrisons",
                columns: new[] { "HostKind", "HostId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClanQuestProgress_ClanId_QuestKey",
                table: "ClanQuestProgress",
                columns: new[] { "ClanId", "QuestKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ClanStructures_ClanId",
                table: "ClanStructures",
                column: "ClanId");

            migrationBuilder.CreateIndex(
                name: "IX_ClanStructures_ServerId_X_Y",
                table: "ClanStructures",
                columns: new[] { "ServerId", "X", "Y" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ClanQuestProgress");

            migrationBuilder.DropTable(
                name: "ClanStructures");

            migrationBuilder.DropIndex(
                name: "IX_Garrisons_HostKind_HostId",
                table: "Garrisons");

            migrationBuilder.DropColumn(
                name: "HostKind",
                table: "Garrisons");

            migrationBuilder.DropColumn(
                name: "ContributionPoints",
                table: "Clans");

            migrationBuilder.DropColumn(
                name: "Contribution",
                table: "ClanMembers");

            migrationBuilder.RenameColumn(
                name: "HostId",
                table: "Garrisons",
                newName: "VillageId");

            migrationBuilder.CreateIndex(
                name: "IX_Garrisons_VillageId",
                table: "Garrisons",
                column: "VillageId",
                unique: true);
        }
    }
}
