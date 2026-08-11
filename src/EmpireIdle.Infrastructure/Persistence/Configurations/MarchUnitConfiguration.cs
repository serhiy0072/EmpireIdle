using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class MarchUnitConfiguration : IEntityTypeConfiguration<MarchUnit>
    {
        public void Configure(EntityTypeBuilder<MarchUnit> builder)
        {
            builder.ToTable("MarchUnits");
            builder.HasKey(u => u.Id);
            builder.Property(u => u.Id).ValueGeneratedNever();

            builder.Property(u => u.UnitType).IsRequired().HasMaxLength(50);

            builder.Ignore(u => u.DomainEvents);
        }
    }
}