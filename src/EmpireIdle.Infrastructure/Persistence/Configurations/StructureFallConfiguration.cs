using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class StructureFallConfiguration : IEntityTypeConfiguration<StructureFall>
{
    public void Configure(EntityTypeBuilder<StructureFall> builder)
    {
        builder.ToTable("StructureFalls");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.AttackerVillageName).IsRequired().HasMaxLength(100);

        // Історія клану — для майбутнього екрана втрат території
        builder.HasIndex(f => new { f.ClanId, f.OccurredAt });

        builder.Ignore(f => f.DomainEvents);
    }
}
