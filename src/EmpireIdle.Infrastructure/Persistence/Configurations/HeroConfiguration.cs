using System.Text.Json;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
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

            // Рівні вмінь — непрозорий для БД JSON: жоден запит не фільтрує за ними (GDD §6.1)
            builder.Property(h => h.SkillLevels)
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(
                    levels => JsonSerializer.Serialize(levels, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<Dictionary<string, int>>(json, (JsonSerializerOptions?)null)
                        ?? new Dictionary<string, int>(),
                    new ValueComparer<IReadOnlyDictionary<string, int>>(
                        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                        levels => levels.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                        levels => new Dictionary<string, int>(levels)));

            // xmin — той самий токен паралелізму, що й у решті агрегатів
            builder.Property(h => h.Version).IsRowVersion();

            builder.HasIndex(h => new { h.PlayerId, h.ServerId });

            // Один екземпляр героя на гравця: дублікат іде в сузір'я, а не другим
            // рядком. Це індекс, а не перевірка в хендлері — паралельні призови
            // інакше створили б двох.
            builder.HasIndex(h => new { h.PlayerId, h.HeroKey }).IsUnique();

            builder.HasIndex(h => h.StationedGarrisonId);

            // Один лідер на гравця в гарнізоні. Ключ включає PlayerId, бо в
            // чужому селі стоять підкріплення кількох союзників, і в кожного
            // свій лідер над своїм стеком.
            //
            // Частковий індекс, а не перевірка в хендлері: два паралельні
            // призначення інакше дали б двох лідерів і подвійний бонус.
            builder.HasIndex(h => new { h.StationedGarrisonId, h.PlayerId })
                .IsUnique()
                .HasFilter("\"IsLeader\" AND \"StationedGarrisonId\" IS NOT NULL");

            builder.Ignore(h => h.DomainEvents);
        }
    }
}
