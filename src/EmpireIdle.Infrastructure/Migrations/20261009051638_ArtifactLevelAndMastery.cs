using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Заточка артефакта розпадається на рівень і майстерність коваля (GDD §6.4, §9.12).
    /// Стара заточка стає рівнем — ролли статів у журналі вже відповідають їй; досвід — накопичений
    /// до цього рівня за кривою round10(40 · n^1.4); майстерність — половина заточки вгору, не вище 10:
    /// 0.05 · L + 0.1 · ⌈L/2⌉ тримає силу предмета близькою до старих 0.1 · L. Поломки більше немає.
    /// </summary>
    public partial class ArtifactLevelAndMastery : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBroken",
                table: "EquipmentItems");

            migrationBuilder.RenameColumn(
                name: "EnhancementLevel",
                table: "EquipmentItems",
                newName: "Level");

            migrationBuilder.AddColumn<long>(
                name: "Experience",
                table: "EquipmentItems",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);

            migrationBuilder.AddColumn<int>(
                name: "Mastery",
                table: "EquipmentItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql("""
                UPDATE "EquipmentItems" SET
                    "Mastery" = LEAST(10, CEIL("Level" / 2.0)),
                    "Experience" = (
                        SELECT COALESCE(SUM(ROUND(40 * power(n, 1.4) / 10) * 10), 0)
                        FROM generate_series(1, "Level") AS n)
                WHERE "Level" > 0;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Experience",
                table: "EquipmentItems");

            migrationBuilder.DropColumn(
                name: "Mastery",
                table: "EquipmentItems");

            migrationBuilder.RenameColumn(
                name: "Level",
                table: "EquipmentItems",
                newName: "EnhancementLevel");

            migrationBuilder.AddColumn<bool>(
                name: "IsBroken",
                table: "EquipmentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }
    }
}
