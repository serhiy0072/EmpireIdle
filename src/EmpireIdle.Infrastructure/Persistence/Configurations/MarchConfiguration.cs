using EmpireIdle.Domain.Entities;
using EmpireIdle.Domain.Enums;
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
            // Сканер шукає дозрілі в межах світу; завершені й табори (стоять до відкликання) не сканує.
            // Фільтр дослівно повторює умову GetDueAsync — інакше планувальник індекс не візьме
            builder.HasIndex(m => new { m.ServerId, m.ArrivesAt })
                .HasFilter($"\"State\" <> {(int)MarchState.Completed} AND \"State\" <> {(int)MarchState.Camping}");

            builder.HasMany(m => m.Units)
                .WithOne()
                .HasForeignKey(u => u.MarchId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasMany(m => m.Cargo)
                .WithOne()
                .HasForeignKey(c => c.MarchId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(m => m.Cargo).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Navigation(m => m.Units).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Property<uint>("Version").IsRowVersion();

            builder.Ignore(m => m.DomainEvents);
        }
    }
}
