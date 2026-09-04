using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanHelpContributionConfiguration : IEntityTypeConfiguration<ClanHelpContribution>
    {
        public void Configure(EntityTypeBuilder<ClanHelpContribution> builder)
        {
            builder.ToTable("ClanHelpContributions");
            builder.HasKey(h => h.Id);

            // Одна допомога від гравця на запит. Структурно, а не перевіркою:
            // два паралельні кліки інакше пройшли б обидва
            builder.HasIndex(h => new { h.RequestId, h.HelperId }).IsUnique();
        }
    }
}
