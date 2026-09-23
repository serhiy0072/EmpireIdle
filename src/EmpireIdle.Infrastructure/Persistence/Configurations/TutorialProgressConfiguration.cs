using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class TutorialProgressConfiguration : IEntityTypeConfiguration<TutorialProgress>
    {
        public void Configure(EntityTypeBuilder<TutorialProgress> builder)
        {
            builder.ToTable("TutorialProgress");
            builder.HasKey(t => t.Id);

            // Один рядок на гравця; індекс — арбітр гонки двох перших кроків з різних вкладок
            builder.HasIndex(t => t.PlayerId).IsUnique();

            // Ключі кроків — text[] Postgres: без окремої таблиці на десяток рядків.
            // Мапимо приватне поле напряму: публічна властивість лише для читання
            builder.Ignore(t => t.SeenSteps);
            builder.Property<List<string>>("_seenSteps")
                .HasColumnName("SeenSteps")
                .IsRequired();

            builder.Property(t => t.Version).IsRowVersion();
        }
    }
}
