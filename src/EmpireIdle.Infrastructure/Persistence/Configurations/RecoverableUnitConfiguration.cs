using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class RecoverableUnitConfiguration : IEntityTypeConfiguration<RecoverableUnit>
    {
        public void Configure(EntityTypeBuilder<RecoverableUnit> builder)
        {
            builder.ToTable("RecoverableUnits");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.UnitType).IsRequired().HasMaxLength(50);

            // Унікального індексу немає: один тип юніта може мати кілька стеків
            // з різними дедлайнами (по одному на бій).
            builder.HasIndex(r => new { r.GarrisonId, r.ExpiresAt });

            builder.Ignore(r => r.DomainEvents);
        }
    }
}