using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class LootBoxProgressConfiguration : IEntityTypeConfiguration<LootBoxProgress>
    {
        public void Configure(EntityTypeBuilder<LootBoxProgress> builder)
        {
            builder.ToTable("LootBoxProgress");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.BoxKey).IsRequired().HasMaxLength(50);
            builder.HasIndex(p => new { p.PlayerId, p.BoxKey }).IsUnique();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}