using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class HeroConfiguration : IEntityTypeConfiguration<Hero>
{
    public void Configure(EntityTypeBuilder<Hero> builder)
    {
        builder.ToTable("Heroes");
        builder.HasKey(h => h.Id);
        builder.Property(h => h.Id).ValueGeneratedNever();

        builder.Property(h => h.HeroKey).IsRequired().HasMaxLength(50);
        builder.Property(h => h.State).HasConversion<int>();

        // xmin — той самий токен паралелізму, що й у решті агрегатів
        builder.Property(h => h.Version).IsRowVersion();

        builder.HasIndex(h => new { h.PlayerId, h.ServerId });

        // Один екземпляр героя на гравця: дублікат іде в сузір'я, а не другим
        // рядком. Це індекс, а не перевірка в хендлері — паралельні призови
        // інакше створили б двох.
        builder.HasIndex(h => new { h.PlayerId, h.HeroKey }).IsUnique();

        builder.Ignore(h => h.DomainEvents);
    }
}

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
