using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class IdempotencyRecordConfiguration : IEntityTypeConfiguration<IdempotencyRecord>
    {
        public void Configure(EntityTypeBuilder<IdempotencyRecord> builder)
        {
            builder.ToTable("IdempotencyRecords");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.Key).IsRequired().HasMaxLength(100);
            builder.Property(r => r.RequestType).IsRequired().HasMaxLength(200);

            // Ключ унікальний у межах гравця — головний захист від дублю
            builder.HasIndex(r => new { r.PlayerId, r.Key }).IsUnique();

            // Для періодичної чистки старих записів
            builder.HasIndex(r => r.CreatedAt);

            builder.Ignore(r => r.DomainEvents);
        }
    }
}