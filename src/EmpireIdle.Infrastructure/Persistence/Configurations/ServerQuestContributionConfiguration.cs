using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ServerQuestContributionConfiguration : IEntityTypeConfiguration<ServerQuestContribution>
    {
        public void Configure(EntityTypeBuilder<ServerQuestContribution> builder)
        {
            builder.HasKey(c => c.Id);
            builder.Ignore(c => c.DomainEvents);

            builder.Property(c => c.QuestKey).IsRequired().HasMaxLength(50);

            builder.HasIndex(c => new { c.ServerId, c.QuestKey, c.PlayerId }).IsUnique();

            // Рангування: без цього індексу топ-100 на 10k учасників = seq scan
            builder.HasIndex(c => new { c.ServerId, c.QuestKey, c.Amount })
                .IsDescending(false, false, true);
        }
    }
}
