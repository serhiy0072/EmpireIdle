using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class TowerProgressConfiguration : IEntityTypeConfiguration<TowerProgress>
    {
        public void Configure(EntityTypeBuilder<TowerProgress> builder)
        {
            builder.ToTable("TowerProgress");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            // Два перші штурми з різних вкладок не створять двох рядків — гонку розв'язує індекс
            builder.HasIndex(p => p.PlayerId).IsUnique();

            // Рейтинг сервера: найвищий поверх, за рівності — хто дійшов раніше
            builder.HasIndex(p => new { p.ServerId, p.HighestFloor, p.ReachedAt }).IsDescending(false, true, false);

            builder.Property(p => p.Version).IsRowVersion();

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
