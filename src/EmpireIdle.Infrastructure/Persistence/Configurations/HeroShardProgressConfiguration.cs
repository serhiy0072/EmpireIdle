using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class HeroShardProgressConfiguration : IEntityTypeConfiguration<HeroShardProgress>
{
    public void Configure(EntityTypeBuilder<HeroShardProgress> builder)
    {
        builder.ToTable("HeroShards");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.HeroKey).IsRequired().HasMaxLength(50);

        // Один рядок на пару гравець-герой. Індексом, а не перевіркою:
        // дві паралельні купівлі інакше створили б два лічильники,
        // і жоден із них не дійшов би до порогу.
        builder.HasIndex(s => new { s.PlayerId, s.HeroKey }).IsUnique();

        builder.Ignore(s => s.DomainEvents);
    }
}
