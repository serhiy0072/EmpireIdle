using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class MarchConfiguration : IEntityTypeConfiguration<March>
    {
        public void Configure(EntityTypeBuilder<March> builder)
        {
            builder.ToTable("Marches");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();

            builder.Property(m => m.State).HasConversion<int>();
            builder.Property(m => m.TargetType).HasConversion<int>();
            builder.Property(m => m.Intent).HasConversion<int>();
            builder.Property(m => m.UpdatedAt).IsRequired();

            builder.HasIndex(m => m.GarrisonId);
            builder.HasIndex(m => new { m.State, m.ArrivesAt }); // сканер шукає дозрілі

            builder.HasMany(m => m.Units)
                .WithOne()
                .HasForeignKey(u => u.MarchId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(m => m.Units).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Property<uint>("Version").IsRowVersion();

            builder.Ignore(m => m.DomainEvents);
        }
    }
}
