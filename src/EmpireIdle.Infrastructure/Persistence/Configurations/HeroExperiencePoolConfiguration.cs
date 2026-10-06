using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class HeroExperiencePoolConfiguration : IEntityTypeConfiguration<HeroExperiencePool>
{
    public void Configure(EntityTypeBuilder<HeroExperiencePool> builder)
    {
        builder.ToTable("HeroExperience");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Нагороди й підняття рівня пишуть той самий рядок паралельно
        builder.Property(p => p.Version).IsRowVersion();

        // Один пул на гравця. Індексом: дві перші нагороди одночасно інакше створили б два пули
        builder.HasIndex(p => p.PlayerId).IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}
