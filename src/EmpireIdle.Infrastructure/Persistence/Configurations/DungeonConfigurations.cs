using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class DungeonRunConfiguration : IEntityTypeConfiguration<DungeonRun>
    {
        public void Configure(EntityTypeBuilder<DungeonRun> builder)
        {
            builder.ToTable("DungeonRuns");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.DungeonKey).IsRequired().HasMaxLength(50);

            // Стан бою — непрозорий для БД JSON: його читає лише рушій,
            // і жоден запит не фільтрує за вмістом бою
            builder.Property(r => r.Battle).IsRequired().HasColumnType("jsonb");

            // Два ходи з двох вкладок інакше прочитали б однаковий стан
            // і обидва зарахували б свою дію
            builder.Property(r => r.Version).IsRowVersion();

            // Один незавершений забіг на гравця: частковий індекс замість перевірки
            // в коді, бо дві вкладки стартують одночасно
            builder.HasIndex(r => r.PlayerId)
                .IsUnique()
                .HasFilter("\"State\" = 0");

            builder.Ignore(r => r.DomainEvents);
        }
    }

    public class DungeonEnergyConfiguration : IEntityTypeConfiguration<DungeonEnergy>
    {
        public void Configure(EntityTypeBuilder<DungeonEnergy> builder)
        {
            builder.ToTable("DungeonEnergy");
            builder.HasKey(e => e.Id);
            builder.Property(e => e.Id).ValueGeneratedNever();

            builder.Property(e => e.Version).IsRowVersion();
            builder.HasIndex(e => e.PlayerId).IsUnique();

            builder.Ignore(e => e.DomainEvents);
        }
    }

    public class DungeonClearConfiguration : IEntityTypeConfiguration<DungeonClear>
    {
        public void Configure(EntityTypeBuilder<DungeonClear> builder)
        {
            builder.ToTable("DungeonClears");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Property(c => c.DungeonKey).IsRequired().HasMaxLength(50);

            // Повторна зачистка того ж рівня нічого не додає — індекс тримає один рядок
            builder.HasIndex(c => new { c.PlayerId, c.DungeonKey, c.Level }).IsUnique();

            builder.Ignore(c => c.DomainEvents);
        }
    }
}
