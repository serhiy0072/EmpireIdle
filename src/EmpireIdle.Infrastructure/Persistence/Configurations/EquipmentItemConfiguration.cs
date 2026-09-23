using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

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

        builder.Property(e => e.Version).IsRowVersion();

        // Окремих індексів по PlayerId і EquippedByHeroId немає навмисно:
        // обидва повністю покриваються префіксами складених нижче, і були б
        // зайвими записами на кожне вдягання
        builder.HasIndex(e => new { e.PlayerId, e.ServerId });

        // Слот зайнятий рівно одним предметом. Арбітр — індекс, а не
        // перевірка в хендлері: два паралельні вдягання інакше дали б
        // героєві дві зброї
        builder.HasIndex(e => new { e.EquippedByHeroId, e.Slot, e.SlotIndex })
            .IsUnique()
            .HasFilter("\"EquippedByHeroId\" IS NOT NULL");

        // Один предмет одного типу на героя. Зі слотами за типом ключ уже
        // однозначно задає слот, тож це страховка на випадок конфіга, де
        // тип предмета змінили, а вдягнені екземпляри лишились у старому слоті
        builder.HasIndex(e => new { e.EquippedByHeroId, e.ItemKey })
            .IsUnique()
            .HasFilter("\"EquippedByHeroId\" IS NOT NULL");

        builder.HasMany(e => e.Stats)
            .WithOne()
            .HasForeignKey(s => s.EquipmentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasMany(e => e.Rolls)
            .WithOne()
            .HasForeignKey(r => r.EquipmentItemId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(e => e.Stats).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.Navigation(e => e.Rolls).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(e => e.DomainEvents);
    }
}
