using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class BeastPenConfiguration : IEntityTypeConfiguration<BeastPen>
{
    public void Configure(EntityTypeBuilder<BeastPen> builder)
    {
        builder.ToTable("BeastPens");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.UpdatedAt).IsRequired();

        // Два марші-приручення одного гравця прибувають одночасно: без токена обидва
        // зайняли б останнє місце звіринця
        builder.Property<uint>("Version").IsRowVersion();

        // Один звіринець на гравця — арбітр гонки першого приручення
        builder.HasIndex(p => p.PlayerId).IsUnique();

        builder.HasMany(p => p.Beasts)
            .WithOne()
            .HasForeignKey(b => b.BeastPenId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Beasts).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasMany(p => p.Pity)
            .WithOne()
            .HasForeignKey(p => p.BeastPenId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(p => p.Pity).UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.Ignore(p => p.DomainEvents);
    }
}

public class BeastConfiguration : IEntityTypeConfiguration<Beast>
{
    public void Configure(EntityTypeBuilder<Beast> builder)
    {
        builder.ToTable("Beasts");
        builder.HasKey(b => b.Id);
        builder.Property(b => b.Id).ValueGeneratedNever();
        builder.Property(b => b.BeastKey).IsRequired().HasMaxLength(50);

        // Вид живе в звіринці один раз — дублікат піднімає ранг, а не додає рядок
        builder.HasIndex(b => new { b.BeastPenId, b.BeastKey }).IsUnique();

        builder.Ignore(b => b.DomainEvents);
    }
}

public class BeastTamingPityConfiguration : IEntityTypeConfiguration<BeastTamingPity>
{
    public void Configure(EntityTypeBuilder<BeastTamingPity> builder)
    {
        builder.ToTable("BeastTamingPity");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();
        builder.Property(p => p.BeastKey).IsRequired().HasMaxLength(50);

        builder.HasIndex(p => new { p.BeastPenId, p.BeastKey }).IsUnique();

        builder.Ignore(p => p.DomainEvents);
    }
}
