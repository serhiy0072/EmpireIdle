using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class MarketPriceSnapshotConfiguration : IEntityTypeConfiguration<MarketPriceSnapshot>
{
    public void Configure(EntityTypeBuilder<MarketPriceSnapshot> builder)
    {
        builder.ToTable("MarketPriceSnapshots");

        // Один знімок на категорію в кожному світі; джоб його переписує
        builder.HasKey(s => new { s.ServerId, s.PricingKey });

        builder.Property(s => s.PricingKey).HasMaxLength(64);
    }
}
