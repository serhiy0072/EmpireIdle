using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanOfferingLedgerConfiguration : IEntityTypeConfiguration<ClanOfferingLedger>
    {
        public void Configure(EntityTypeBuilder<ClanOfferingLedger> builder)
        {
            builder.ToTable("ClanOfferingLedgers");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            // Два перші підношення з різних вкладок не створять двох книг — гонку розв'язує індекс
            builder.HasIndex(l => l.PlayerId).IsUnique();

            ClanResearchConfiguration.ConfigureLevels(builder.Property(l => l.ShopPurchases));

            builder.Property(l => l.Version).IsRowVersion();

            builder.Ignore(l => l.DomainEvents);
        }
    }

    public class ClanContributionConfiguration : IEntityTypeConfiguration<ClanContribution>
    {
        public void Configure(EntityTypeBuilder<ClanContribution> builder)
        {
            builder.ToTable("ClanContributions");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            // Один рядок на пару: повернення в клан продовжує старий внесок
            builder.HasIndex(c => new { c.ClanId, c.PlayerId }).IsUnique();

            builder.Ignore(c => c.DomainEvents);
        }
    }
}
