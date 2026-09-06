using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanHelpRequestConfiguration : IEntityTypeConfiguration<ClanHelpRequest>
    {
        public void Configure(EntityTypeBuilder<ClanHelpRequest> builder)
        {
            builder.ToTable("ClanHelpRequests");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.TargetType).HasConversion<int>();

            // Один запит на ціль: другий засмітив би список клану дублем
            builder.HasIndex(r => r.TargetId).IsUnique();

            // Список допомоги читається за кланом і строком
            builder.HasIndex(r => new { r.ClanId, r.ExpiresAt });

            builder.HasMany(r => r.Helpers)
                .WithOne()
                .HasForeignKey(h => h.RequestId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(r => r.Helpers).UsePropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
