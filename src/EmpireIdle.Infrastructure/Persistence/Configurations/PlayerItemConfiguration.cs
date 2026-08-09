using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

public class PlayerItemConfiguration : IEntityTypeConfiguration<PlayerItem>
{
    public void Configure(EntityTypeBuilder<PlayerItem> builder)
    {
        builder.ToTable("PlayerItems");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).ValueGeneratedNever();

        builder.Property(i => i.ItemKey).IsRequired().HasMaxLength(50);
        // Один стек на тип предмета в межах гравця
        builder.HasIndex(i => new { i.PlayerId, i.ItemKey }).IsUnique();

        builder.Ignore(i => i.DomainEvents);
    }
}
