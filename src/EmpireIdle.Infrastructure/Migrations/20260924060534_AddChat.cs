using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddChat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Players",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "uk");

            migrationBuilder.CreateTable(
                name: "ChatMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ServerId = table.Column<int>(type: "integer", nullable: false),
                    Channel = table.Column<int>(type: "integer", nullable: false),
                    ClanId = table.Column<Guid>(type: "uuid", nullable: true),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    RecipientId = table.Column<Guid>(type: "uuid", nullable: true),
                    Text = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatMessages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ChatTranslations",
                columns: table => new
                {
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    Language = table.Column<string>(type: "character varying(8)", maxLength: 8, nullable: false),
                    Text = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatTranslations", x => new { x.MessageId, x.Language });
                    table.ForeignKey(
                        name: "FK_ChatTranslations_ChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "ChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ClanId_SentAt",
                table: "ChatMessages",
                columns: new[] { "ClanId", "SentAt" },
                filter: "\"ClanId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_RecipientId_SentAt",
                table: "ChatMessages",
                columns: new[] { "RecipientId", "SentAt" },
                filter: "\"RecipientId\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SenderId_SentAt",
                table: "ChatMessages",
                columns: new[] { "SenderId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_SentAt",
                table: "ChatMessages",
                column: "SentAt");

            migrationBuilder.CreateIndex(
                name: "IX_ChatMessages_ServerId_Channel_SentAt",
                table: "ChatMessages",
                columns: new[] { "ServerId", "Channel", "SentAt" },
                filter: "\"Channel\" = 1");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatTranslations");

            migrationBuilder.DropTable(
                name: "ChatMessages");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Players");
        }
    }
}
