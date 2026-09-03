using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanConfiguration : IEntityTypeConfiguration<Clan>
    {
        public void Configure(EntityTypeBuilder<Clan> builder)
        {
            builder.ToTable("Clans");
            builder.HasKey(c => c.Id);

            builder.Property(c => c.Name).IsRequired().HasMaxLength(32);
            builder.Property(c => c.Tag).IsRequired().HasMaxLength(5);
            builder.Property(c => c.Description).HasMaxLength(512);
            builder.Property(c => c.JoinPolicy).HasConversion<int>();

            // Назва й тег унікальні в межах світу: гравці розрізняють клани
            // саме за ними, і два однакові зробили б чат і карту нечитабельними
            builder.HasIndex(c => new { c.ServerId, c.Name }).IsUnique();
            builder.HasIndex(c => new { c.ServerId, c.Tag }).IsUnique();

            builder.HasMany(c => c.Members)
                .WithOne()
                .HasForeignKey(m => m.ClanId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(c => c.Roles)
                .WithOne()
                .HasForeignKey(r => r.ClanId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(c => c.Members).UsePropertyAccessMode(PropertyAccessMode.Field);
            builder.Navigation(c => c.Roles).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Property(c => c.Version).IsRowVersion();
        }
    }
}
