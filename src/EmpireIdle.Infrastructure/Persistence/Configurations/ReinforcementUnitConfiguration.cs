using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ReinforcementUnitConfiguration : IEntityTypeConfiguration<ReinforcementUnit>
    {
        public void Configure(EntityTypeBuilder<ReinforcementUnit> builder)
        {
            builder.ToTable("ReinforcementUnits");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.UnitType).IsRequired().HasMaxLength(50);

            // Один стек на трійку: два рядки з тим самим власником і типом
            // розійшлися б у підрахунку втрат
            builder.HasIndex(r => new { r.GarrisonId, r.OwnerPlayerId, r.UnitType }).IsUnique();

            // Пошук «де стоять мої війська» — по власнику
            builder.HasIndex(r => r.OwnerPlayerId);

            builder.Ignore(r => r.DomainEvents);
        }
    }
}
