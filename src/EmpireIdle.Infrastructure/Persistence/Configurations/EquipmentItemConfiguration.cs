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
        builder.Property(e => e.Rarity).HasConversion<int>();
        builder.Property(e => e.Slot).HasConversion<int>();

        builder.HasIndex(e => e.PlayerId);
        builder.HasIndex(e => e.EquippedByHeroId);

        builder.Property(e => e.Version).IsRowVersion();

        builder.HasIndex(e => new { e.PlayerId, e.ServerId });

        // Слот зайнятий рівно одним предметом. Арбітр — індекс, а не
        // перевірка в хендлері: два паралельні вдягання інакше дали б
        // героєві дві зброї
        builder.HasIndex(e => new { e.EquippedByHeroId, e.Slot, e.SlotIndex })
            .IsUnique()
            .HasFilter("\"EquippedByHeroId\" IS NOT NULL");

        builder.HasMany(e => e.Stats)
            .WithOne()
            .HasForeignKey(s => s.EquipmentItemId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(e => e.Stats).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.DomainEvents);
    }
}
