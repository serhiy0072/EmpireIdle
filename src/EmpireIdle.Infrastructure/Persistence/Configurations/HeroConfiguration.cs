using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;


namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
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
}
