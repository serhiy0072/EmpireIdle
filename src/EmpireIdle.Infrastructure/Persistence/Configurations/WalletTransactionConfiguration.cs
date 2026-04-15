using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF Core конфігурація для WalletTransaction entity.
    /// </summary>
    public class WalletTransactionConfiguration : IEntityTypeConfiguration<WalletTransaction>
    {
        public void Configure(EntityTypeBuilder<WalletTransaction> builder)
        {
            builder.HasKey(t => t.Id);

            builder.Property(t => t.Reference).IsRequired().HasMaxLength(200);
            builder.Property(t => t.Amount).IsRequired();
            builder.Property(t => t.Type).IsRequired();

            builder.ToTable("WalletTransactions");
        }
    }
}
