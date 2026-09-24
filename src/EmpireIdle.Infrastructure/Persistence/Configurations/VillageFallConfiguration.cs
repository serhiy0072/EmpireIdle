using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class VillageFallConfiguration : IEntityTypeConfiguration<VillageFall>
{
    public void Configure(EntityTypeBuilder<VillageFall> builder)
    {
        builder.ToTable("VillageFalls");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).ValueGeneratedNever();
        builder.Property(f => f.AttackerVillageName).IsRequired().HasMaxLength(100);

        // Ліміт виселень нападника за вікно
        builder.HasIndex(f => new { f.AttackerPlayerId, f.OccurredAt });

        // Історія виселеного — для скарг і майбутнього екрана
        builder.HasIndex(f => new { f.PlayerId, f.OccurredAt });

        builder.Ignore(f => f.DomainEvents);
    }
}
