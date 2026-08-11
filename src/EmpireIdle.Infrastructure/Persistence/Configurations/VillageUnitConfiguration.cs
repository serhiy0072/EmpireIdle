using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class VillageUnitConfiguration : IEntityTypeConfiguration<VillageUnit>
{
    public void Configure(EntityTypeBuilder<VillageUnit> builder)
    {
        builder.ToTable("VillageUnits");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).ValueGeneratedNever();
        builder.Property(u => u.UnitType).IsRequired().HasMaxLength(50);
        builder.HasIndex(u => new { u.GarrisonId, u.UnitType }).IsUnique();
    }
}
