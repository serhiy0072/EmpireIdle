using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class WoundedUnitConfiguration : IEntityTypeConfiguration<WoundedUnit>
    {
        public void Configure(EntityTypeBuilder<WoundedUnit> builder)
        {
            builder.ToTable("WoundedUnits");
            builder.HasKey(w => w.Id);
            builder.Property(w => w.Id).ValueGeneratedNever();

            builder.Property(w => w.UnitType).IsRequired().HasMaxLength(50);
            builder.HasIndex(w => new { w.GarrisonId, w.UnitType }).IsUnique();

            builder.Ignore(w => w.DomainEvents);
        }
    }
}
