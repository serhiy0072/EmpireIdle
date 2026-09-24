using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class MailLetterConfiguration : IEntityTypeConfiguration<MailLetter>
{
    public void Configure(EntityTypeBuilder<MailLetter> builder)
    {
        builder.ToTable("MailLetters");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).ValueGeneratedNever();
        builder.Property(l => l.Kind).HasConversion<int>();

        // Скринька гравця й лічильник непрочитаних
        builder.HasIndex(l => new { l.PlayerId, l.ExpiresAt });

        // Джоб прибирання
        builder.HasIndex(l => l.ExpiresAt);

        builder.Ignore(l => l.DomainEvents);
    }
}

public class AnnouncementConfiguration : IEntityTypeConfiguration<Announcement>
{
    public void Configure(EntityTypeBuilder<Announcement> builder)
    {
        builder.ToTable("Announcements");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();
        builder.Property(a => a.Kind).HasConversion<int>();
        builder.Property(a => a.Title).IsRequired().HasMaxLength(200);
        builder.Property(a => a.Body).IsRequired().HasMaxLength(8000);

        builder.HasIndex(a => new { a.ServerId, a.ExpiresAt });

        builder.Ignore(a => a.DomainEvents);
    }
}

public class AnnouncementReadConfiguration : IEntityTypeConfiguration<AnnouncementRead>
{
    public void Configure(EntityTypeBuilder<AnnouncementRead> builder)
    {
        builder.ToTable("AnnouncementReads");

        // Одна мітка на гравця й оголошення; повторне відкриття її не дублює
        builder.HasKey(r => new { r.AnnouncementId, r.PlayerId });

        builder.HasOne<Announcement>()
            .WithMany()
            .HasForeignKey(r => r.AnnouncementId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
