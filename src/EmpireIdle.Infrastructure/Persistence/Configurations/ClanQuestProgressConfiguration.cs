using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanQuestProgressConfiguration : IEntityTypeConfiguration<ClanQuestProgress>
    {
        public void Configure(EntityTypeBuilder<ClanQuestProgress> builder)
        {
            builder.ToTable("ClanQuestProgress");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever(); // ключ ставить домен, не БД/EF

            builder.Property(p => p.QuestKey).IsRequired().HasMaxLength(50);
            builder.Property(p => p.State).HasConversion<int>();

            // Внески учасників приходять паралельно — другий мусить перечитати
            builder.Property(p => p.Version).IsRowVersion();

            // Один рядок на клан і квест: перший внесок двох учасників одночасно розв'язує індекс
            builder.HasIndex(p => new { p.ClanId, p.QuestKey }).IsUnique();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
