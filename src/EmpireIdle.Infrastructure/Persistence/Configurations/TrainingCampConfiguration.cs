using System.Text.Json;
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class TrainingCampConfiguration : IEntityTypeConfiguration<TrainingCamp>
{
    public void Configure(EntityTypeBuilder<TrainingCamp> builder)
    {
        builder.ToTable("TrainingCamps");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        // Перезарядки слотів — непрозорий для БД JSON: жоден запит за ними не фільтрує
        builder.Property(c => c.SlotCooldowns)
            .HasColumnType("jsonb")
            .HasConversion(
                cooldowns => JsonSerializer.Serialize(cooldowns, (JsonSerializerOptions?)null),
                json => JsonSerializer.Deserialize<Dictionary<int, DateTime>>(json, (JsonSerializerOptions?)null)
                    ?? new Dictionary<int, DateTime>(),
                new ValueComparer<IReadOnlyDictionary<int, DateTime>>(
                    (a, b) => a!.Count == b!.Count && !a.Except(b).Any(),
                    cooldowns => cooldowns.Aggregate(0, (hash, pair) => HashCode.Combine(hash, pair.Key, pair.Value)),
                    cooldowns => new Dictionary<int, DateTime>(cooldowns)));

        // Купівля слота й вихід героя пишуть той самий рядок паралельно
        builder.Property(c => c.Version).IsRowVersion();

        // Один табір на гравця. Індексом: два перші звернення одночасно інакше створили б два табори
        builder.HasIndex(c => c.PlayerId).IsUnique();

        builder.Ignore(c => c.DomainEvents);
    }
}
