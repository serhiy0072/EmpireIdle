

using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF Core конфігурація для PlayerWallet aggregate.
    /// </summary>
    public class PlayerWalletConfiguration : IEntityTypeConfiguration<PlayerWallet>
    {
        public void Configure(EntityTypeBuilder<PlayerWallet> builder)
        {
            builder.HasKey(pw => pw.Id);

            builder.Property(pw => pw.GemBalance).HasConversion(g => g.Value, v => new GemAmount(v)).HasColumnName("GemBalance");
            builder.Property(pw => pw.CoinBalance).HasConversion(c => c.Value, v => new CoinAmount(v)).HasColumnName("CoinBalance");
            builder.HasMany(typeof(WalletTransaction), "_walletTransactions").WithOne().HasForeignKey("WalletId").IsRequired();
        }
    }
}
