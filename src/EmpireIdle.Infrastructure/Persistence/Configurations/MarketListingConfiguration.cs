using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class MarketListingConfiguration : IEntityTypeConfiguration<MarketListing>
{
    public void Configure(EntityTypeBuilder<MarketListing> builder)
    {
        builder.ToTable("MarketListings");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();

        builder.Property(l => l.Kind).HasConversion<int>();
        builder.Property(l => l.State).HasConversion<int>();
        builder.Property(l => l.ItemKey).IsRequired().HasMaxLength(50);
        builder.Property(l => l.PricingKey).IsRequired().HasMaxLength(64);

        // Дві паралельні покупки одного лота розводить xmin: друга бачить конфлікт
        builder.Property(l => l.Version).IsRowVersion();

        var active = $"\"State\" = {(int)MarketListingState.Active}";

        // Один активний лот на екземпляр і на героя. Арбітр — індекс: два
        // паралельні виставлення інакше продали б той самий меч двічі
        builder.HasIndex(l => l.EquipmentId).IsUnique().HasFilter($"\"EquipmentId\" IS NOT NULL AND {active}");
        builder.HasIndex(l => l.HeroId).IsUnique().HasFilter($"\"HeroId\" IS NOT NULL AND {active}");

        // Сканер строків дивиться лише на активні
        builder.HasIndex(l => l.ExpiresAt).HasFilter(active);

        // Ліміт лотів і «мої лоти»
        builder.HasIndex(l => new { l.ServerId, l.SellerId, l.State });

        // Вітрина фільтрує за видом і товаром серед активних
        builder.HasIndex(l => new { l.ServerId, l.Kind, l.ItemKey }).HasFilter(active);

        // Медіана — продажі категорії за вікно
        builder.HasIndex(l => new { l.ServerId, l.PricingKey, l.ClosedAt })
            .HasFilter($"\"State\" = {(int)MarketListingState.Sold}");

        builder.Ignore(l => l.DomainEvents);
    }
}
