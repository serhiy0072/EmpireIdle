using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class KeyChestProgressConfiguration : IEntityTypeConfiguration<KeyChestProgress>
    {
        public void Configure(EntityTypeBuilder<KeyChestProgress> builder)
        {
            builder.ToTable("KeyChestProgress");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.ChestKey).IsRequired().HasMaxLength(50);

            // Два паралельні кліки інакше взяли б одне безкоштовне відкриття двічі
            builder.Property(p => p.Version).IsRowVersion();

            // Один рядок на пару гравець-скриня: перше відкриття у двох вкладках створило б два
            builder.HasIndex(p => new { p.PlayerId, p.ChestKey }).IsUnique();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
