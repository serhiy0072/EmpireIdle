using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class BannerPityProgressConfiguration : IEntityTypeConfiguration<BannerPityProgress>
    {
        public void Configure(EntityTypeBuilder<BannerPityProgress> builder)
        {
            builder.ToTable("BannerPity");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.PityGroup).IsRequired().HasMaxLength(50);

            // Два паралельні ролли інакше прочитали б однаковий лічильник
            // і обидва списали б одну гарантію
            builder.Property(p => p.Version).IsRowVersion();

            // Один рядок на пару гравець-група. Індексом, а не перевіркою в коді:
            // перший ролл у двох вкладках створив би два лічильники
            builder.HasIndex(p => new { p.PlayerId, p.PityGroup }).IsUnique();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
