using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace EmpireIdle.Infrastructure.Migrations
{
    /// <summary>
    /// Поломка артефакта повертається (GDD §9.12): невдала заточка на пізніх рангах ламає предмет,
    /// і він дає половину статів до ремонту. Наявні предмети — цілі.
    /// </summary>
    public partial class ArtifactBreaking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsBroken",
                table: "EquipmentItems",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsBroken",
                table: "EquipmentItems");
        }
    }
}
