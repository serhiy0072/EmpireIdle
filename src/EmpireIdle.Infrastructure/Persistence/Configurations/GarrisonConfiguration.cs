using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class GarrisonConfiguration : IEntityTypeConfiguration<Garrison>
    {
        public void Configure(EntityTypeBuilder<Garrison> builder)
        {
            builder.ToTable("Garrisons");
            builder.HasKey(g => g.Id);
            builder.Property(g => g.Id).ValueGeneratedNever();

            builder.HasIndex(g => g.VillageId).IsUnique();

            builder.HasMany(g=>g.Units)
                .WithOne()
                .HasForeignKey(u => u.GarrisonId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(g => g.Units).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(g => g.TrainingOrders)
                .WithOne()
                .HasForeignKey(o => o.GarrisonId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(g=>g.TrainingOrders).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(g => g.Wounded)
                .WithOne()
                .HasForeignKey(w => w.GarrisonId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(g => g.Wounded).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.HasMany(g => g.Recoverable)
                .WithOne()
                .HasForeignKey(r => r.GarrisonId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(g => g.Recoverable).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(g => g.DomainEvents);
        }
    }
}
