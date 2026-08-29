using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class PlayerPowerConfiguration : IEntityTypeConfiguration<PlayerPower>
    {
        public void Configure(EntityTypeBuilder<PlayerPower> builder)
        {
            builder.ToTable("PlayerPowers");
            builder.HasKey(p => p.Id);

            // Один рядок на гравця — інакше лідерборд рахував би дублі
            builder.HasIndex(p => p.PlayerId).IsUnique();

            // Лідерборд читає цей індекс і не сортує таблицю
            builder.HasIndex(p => new { p.ServerId, p.TotalPower })
                .IsDescending(false, true);

            builder.Property(p => p.Version).IsRowVersion();
        }
    }
}
