using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class EquipmentItemConfiguration : IEntityTypeConfiguration<EquipmentItem>
{
    public void Configure(EntityTypeBuilder<EquipmentItem> builder)
    {
        builder.ToTable("EquipmentItems");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.ItemKey).IsRequired().HasMaxLength(50);
        builder.Property(e => e.Rarity).IsRequired().HasMaxLength(20);
        builder.Property(e => e.Slot).HasConversion<int>();

        builder.HasIndex(e => e.PlayerId);
        builder.HasIndex(e => e.EquippedByHeroId);

        builder.HasMany(e => e.Stats)
            .WithOne()
            .HasForeignKey(s => s.EquipmentItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Stats).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.DomainEvents);
    }
}
