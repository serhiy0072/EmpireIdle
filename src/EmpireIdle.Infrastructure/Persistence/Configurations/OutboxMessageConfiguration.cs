using EmpireIdle.Infrastructure.Persistence.Outbox;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class OutboxMessageConfiguration : IEntityTypeConfiguration<OutboxMessage>
    {
        public void Configure(EntityTypeBuilder<OutboxMessage> builder)
        {
            builder.HasKey(m => m.Id);

            builder.Property(m => m.Type).IsRequired().HasMaxLength(300);
            builder.Property(m => m.Payload).IsRequired();

            // Воркер бере лише необроблені — частковий індекс тримає його малим
            builder.HasIndex(m => m.OccurredAt)
                .HasFilter("\"ProcessedAt\" IS NULL");
        }
    }
}
