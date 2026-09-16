using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class EquipmentRollConfiguration : IEntityTypeConfiguration<EquipmentRoll>
{
    public void Configure(EntityTypeBuilder<EquipmentRoll> builder)
    {
        // Ім'я явно: DbSet у роллів немає, бо писати їх повз предмет не можна,
        // тож EF узяв би назву з імені типу й дав однину проти решти схеми
        builder.ToTable("EquipmentRolls", "public");

        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        // Журнал читається разом із предметом і в порядку прокачки
        builder.HasIndex(r => new { r.EquipmentItemId, r.Level });
    }
}
