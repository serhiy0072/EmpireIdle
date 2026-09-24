using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class ChatMessageConfiguration : IEntityTypeConfiguration<ChatMessage>
{
    public void Configure(EntityTypeBuilder<ChatMessage> builder)
    {
        builder.ToTable("ChatMessages");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).ValueGeneratedNever();

        builder.Property(m => m.Channel).HasConversion<int>();

        // Межа довжини живе в конфігу; колонка — із запасом на її зміну
        builder.Property(m => m.Text).IsRequired().HasMaxLength(2000);
        builder.Property(m => m.Language).IsRequired().HasMaxLength(8);

        // Історія кожного каналу читається «останні N до моменту» — індекс на канал
        builder.HasIndex(m => new { m.ServerId, m.Channel, m.SentAt })
            .HasFilter($"\"Channel\" = {(int)ChatChannel.Server}");
        builder.HasIndex(m => new { m.ClanId, m.SentAt }).HasFilter("\"ClanId\" IS NOT NULL");

        // Приватна розмова — з обох боків; SenderId+SentAt ще й рахує антиспам
        builder.HasIndex(m => new { m.SenderId, m.SentAt });
        builder.HasIndex(m => new { m.RecipientId, m.SentAt }).HasFilter("\"RecipientId\" IS NOT NULL");

        // Джоб прибирання старої історії
        builder.HasIndex(m => m.SentAt);

        builder.Ignore(m => m.DomainEvents);
    }
}

public class ChatTranslationConfiguration : IEntityTypeConfiguration<ChatTranslation>
{
    public void Configure(EntityTypeBuilder<ChatTranslation> builder)
    {
        builder.ToTable("ChatTranslations");

        // Одна пара «повідомлення + мова» — один переклад; гонку двох читачів розводить ключ
        builder.HasKey(t => new { t.MessageId, t.Language });

        builder.Property(t => t.Language).HasMaxLength(8);
        builder.Property(t => t.Text).IsRequired().HasMaxLength(4000);

        // Повідомлення прибрали — перекладу нема чого лишатись
        builder.HasOne<ChatMessage>()
            .WithMany()
            .HasForeignKey(t => t.MessageId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
