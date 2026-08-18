using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ServerQuestProgressConfiguration : IEntityTypeConfiguration<ServerQuestProgress>
    {
        public void Configure(EntityTypeBuilder<ServerQuestProgress> builder)
        {
            builder.HasKey(q => q.Id);
            builder.Ignore(q => q.DomainEvents);

            builder.Property(q => q.QuestKey).IsRequired().HasMaxLength(50);
            builder.HasIndex(q => new { q.ServerId, q.QuestKey }).IsUnique();
        }
    }
}
