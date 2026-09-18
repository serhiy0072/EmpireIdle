using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class BannerRollRecordConfiguration : IEntityTypeConfiguration<BannerRollRecord>
    {
        public void Configure(EntityTypeBuilder<BannerRollRecord> builder)
        {
            builder.ToTable("BannerRolls");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.BannerKey).IsRequired().HasMaxLength(50);
            builder.Property(r => r.PityGroup).IsRequired().HasMaxLength(50);
            builder.Property(r => r.DropKey).IsRequired().HasMaxLength(50);

            // Типовий запит підтримки — «останні ролли цього гравця»
            builder.HasIndex(r => new { r.PlayerId, r.RolledAt }).IsDescending(false, true);

            builder.Ignore(r => r.DomainEvents);
        }
    }
}
