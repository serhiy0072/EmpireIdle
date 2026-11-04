using System.Text.Json;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanResearchConfiguration : IEntityTypeConfiguration<ClanResearch>
    {
        public void Configure(EntityTypeBuilder<ClanResearch> builder)
        {
            builder.ToTable("ClanResearch");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            // Два перші вибори цілі не створять двох рядків — гонку розв'язує індекс
            builder.HasIndex(r => r.ClanId).IsUnique();

            ConfigureLevels(builder.Property(r => r.Levels));
            ConfigureLevels(builder.Property(r => r.Progress));

            builder.Property(r => r.TargetKey).HasMaxLength(80);

            builder.Property(r => r.Version).IsRowVersion();

            builder.Ignore(r => r.DomainEvents);
        }

        /// <summary>Словник ключів — непрозорий для БД JSON, як у особистих технологій.</summary>
        private static void ConfigureLevels(PropertyBuilder<IReadOnlyDictionary<string, int>> property)
            => property
                .HasColumnType("jsonb")
                .HasDefaultValueSql("'{}'::jsonb")
                .HasConversion(
                    values => JsonSerializer.Serialize(values, (JsonSerializerOptions?)null),
                    json => JsonSerializer.Deserialize<Dictionary<string, int>>(json, (JsonSerializerOptions?)null)
                        ?? new Dictionary<string, int>(),
                    new ValueComparer<IReadOnlyDictionary<string, int>>(
                        (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                        values => values.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                        values => new Dictionary<string, int>(values)));
    }
}
