using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class QuestProgressConfiguration : IEntityTypeConfiguration<QuestProgress>
    {
        public void Configure(EntityTypeBuilder<QuestProgress> builder)
        {
            builder.HasKey(q => q.Id);
            builder.Ignore(q => q.DomainEvents);

            builder.Property(q => q.QuestKey).IsRequired().HasMaxLength(50);
            builder.Property<uint>("Version").IsRowVersion();

            // Один запис на квест у гравця — гарантія проти дублювання при гонці
            builder.HasIndex(q => new { q.PlayerId, q.QuestKey }).IsUnique();
            builder.HasIndex(q => q.ServerId);

            builder.HasMany(q => q.Objectives).WithOne().HasForeignKey(o => o.QuestProgressId).IsRequired();
            builder.Metadata.FindNavigation(nameof(QuestProgress.Objectives))!
                .SetPropertyAccessMode(PropertyAccessMode.Field);
        }
    }
}
