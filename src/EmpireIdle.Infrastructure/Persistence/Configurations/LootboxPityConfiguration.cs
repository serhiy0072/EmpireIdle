using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class LootboxPityConfiguration : IEntityTypeConfiguration<LootboxPity>
    {
        public void Configure(EntityTypeBuilder<LootboxPity> builder)
        {
            builder.ToTable("LootboxPity");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.LootboxKey).IsRequired().HasMaxLength(50);

            // Два паралельні відкриття інакше прочитали б однаковий лічильник
            // і обидва списали б одну гарантію
            builder.Property(p => p.Version).IsRowVersion();

            // Один рядок на пару гравець-скриня: перше відкриття у двох вкладках створило б два лічильники
            builder.HasIndex(p => new { p.PlayerId, p.LootboxKey }).IsUnique();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
