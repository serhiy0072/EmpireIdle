using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class PaymentConfiguration : IEntityTypeConfiguration<Payment>
    {
        public void Configure(EntityTypeBuilder<Payment> builder)
        {
            builder.ToTable("Payments");
            builder.HasKey(p => p.Id);
            builder.Property(p => p.Id).ValueGeneratedNever();

            builder.Property(p => p.Status).HasConversion<int>();
            builder.Property(p => p.PackKey).IsRequired().HasMaxLength(50);
            builder.Property(p => p.Currency).IsRequired().HasMaxLength(3);
            builder.Property(p => p.SessionId).IsRequired().HasMaxLength(255);
            builder.Property<uint>("Version").IsRowVersion();

            // Вебхук знаходить платіж за SessionId — має бути унікальним і швидким
            builder.HasIndex(p => p.SessionId).IsUnique();
            builder.HasIndex(p => new { p.PlayerId, p.CreatedAt });
            builder.HasIndex(p => p.ServerId);

            builder.Ignore(p => p.DomainEvents);
        }
    }
}
