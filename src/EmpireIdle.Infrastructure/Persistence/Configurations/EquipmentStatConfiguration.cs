using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EquipmentStatConfiguration : IEntityTypeConfiguration<EquipmentStat>
{
    public void Configure(EntityTypeBuilder<EquipmentStat> builder)
    {
        builder.ToTable("EquipmentStats");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();
        builder.Property(s => s.StatKey).IsRequired().HasMaxLength(30);

        builder.Ignore(s => s.DomainEvents);
    }
}