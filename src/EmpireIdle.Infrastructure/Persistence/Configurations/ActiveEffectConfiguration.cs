using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ActiveEffectConfiguration : IEntityTypeConfiguration<ActiveEffect>
    {
        public void Configure(EntityTypeBuilder<ActiveEffect> builder)
        {
            builder.ToTable("ActiveEffects");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Target).HasConversion<int>();
            builder.Property(e => e.SourceItemKey).IsRequired().HasMaxLength(50);
            builder.Property<uint>("Version").IsRowVersion();

            // Один активний ефект на ціль у межах гравця — повторний буст продовжує наявний
            builder.HasIndex(e => new { e.PlayerId, e.Target }).IsUnique();

            builder.Ignore(e => e.DomainEvents);
        }
    }
}
