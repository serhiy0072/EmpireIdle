using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class QuestObjectiveProgressConfiguration : IEntityTypeConfiguration<QuestObjectiveProgress>
    {
        public void Configure(EntityTypeBuilder<QuestObjectiveProgress> builder)
        {
            builder.HasKey(o => new { o.QuestProgressId, o.Index });
        }
    }
}
