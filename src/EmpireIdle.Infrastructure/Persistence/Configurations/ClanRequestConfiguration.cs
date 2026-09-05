using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanRequestConfiguration : IEntityTypeConfiguration<ClanRequest>
    {
        public void Configure(EntityTypeBuilder<ClanRequest> builder)
        {
            builder.ToTable("ClanRequests");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Kind).HasConversion<int>();
            builder.Property(r => r.Status).HasConversion<int>();

            // Одна відкрита заявка на трійку. Частковий індекс, а не повний:
            // історія відмов має накопичуватись і не конфліктувати з новою заявкою
            builder.HasIndex(r => new { r.ClanId, r.PlayerId, r.Kind })
                .IsUnique()
                .HasFilter("\"Status\" = 0");

            // Списки «мої запрошення» і «заявки клану» ходять по цих двох
            builder.HasIndex(r => new { r.PlayerId, r.Status });
            builder.HasIndex(r => new { r.ClanId, r.Status });

            builder.Ignore(r => r.DomainEvents);
        }
    }
}
