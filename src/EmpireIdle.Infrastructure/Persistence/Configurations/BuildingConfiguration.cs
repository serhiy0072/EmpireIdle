
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF Core конфігурація для Building entity.
    /// </summary>
    public class BuildingConfiguration : IEntityTypeConfiguration<Building>
    {
        public void Configure(EntityTypeBuilder<Building> builder)
        {
            builder.HasKey(b => b.Id);

            builder.Property(b => b.Type).IsRequired().HasMaxLength(50);
            builder.Property(b => b.Level).HasConversion(l=> l.Value, v=>new Domain.ValueObjects.BuildingLevel(v)).HasColumnName("Level");
        }
    }
}
