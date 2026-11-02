using System.Text.Json;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class PlayerResearchConfiguration : IEntityTypeConfiguration<PlayerResearch>
    {
        public void Configure(EntityTypeBuilder<PlayerResearch> builder)
        {
            builder.ToTable("PlayerResearch");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            // Два перші дослідження з різних вкладок не створять двох рядків — гонку розв'язує індекс
            builder.HasIndex(r => r.PlayerId).IsUnique();

            // Сканер таймерів шукає дозрілі дослідження світу
            builder.HasIndex(r => new { r.ServerId, r.CompletesAt });

            // Рівні — непрозорий для БД JSON, як рівні вмінь героя: жоден запит не фільтрує за ними
            builder.Property(r => r.Levels)
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

            builder.Property(r => r.ActiveKey).HasMaxLength(80);

            builder.Property(r => r.Version).IsRowVersion();

            builder.Ignore(r => r.DomainEvents);
        }
    }
}
