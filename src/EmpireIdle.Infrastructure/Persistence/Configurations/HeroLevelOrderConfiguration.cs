using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class HeroLevelOrderConfiguration : IEntityTypeConfiguration<HeroLevelOrder>
{
    public void Configure(EntityTypeBuilder<HeroLevelOrder> builder)
    {
        builder.ToTable("HeroLevelOrders");
        builder.HasKey(o => o.Id);
        builder.Property(o => o.Id).ValueGeneratedNever();

        builder.HasOne<Hero>()
            .WithMany()
            .HasForeignKey(o => o.HeroId)
            .OnDelete(DeleteBehavior.Cascade);

        // Одне активне замовлення на гравця. Рядок живе лише поки черга йде:
        // сканер таймерів його видаляє, тому звичайного унікального індексу
        // досить — часткового не потрібно.
        builder.HasIndex(o => o.PlayerId).IsUnique();

        // Сканер бере найближчі за часом по кожному світу
        builder.HasIndex(o => new { o.ServerId, o.CompletesAt });

        builder.Ignore(o => o.DomainEvents);
    }
}
