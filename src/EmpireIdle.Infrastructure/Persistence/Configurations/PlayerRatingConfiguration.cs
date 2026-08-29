using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class PlayerRatingConfiguration : IEntityTypeConfiguration<PlayerRating>
    {
        public void Configure(EntityTypeBuilder<PlayerRating> builder)
        {
            builder.ToTable("PlayerRatings");
            builder.HasKey(r => r.Id);

            builder.HasIndex(r => r.PlayerId).IsUnique();

            // Топ читає цей індекс і не сортує таблицю
            builder.HasIndex(r => new { r.ServerId, r.TotalRating })
                .IsDescending(false, true);

            builder.Property(r => r.Version).IsRowVersion();
        }
    }
}
