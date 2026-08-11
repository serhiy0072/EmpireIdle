using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class VillageZoneConfiguration : IEntityTypeConfiguration<VillageZone>
    {
        public void Configure(EntityTypeBuilder<VillageZone> builder)
        {
            builder.ToTable("VillageZones");
            builder.HasKey(z => z.Id);
            builder.Property(z => z.Id).ValueGeneratedNever(); // ручний Guid — пам'ятаємо баг

            builder.Property(z => z.Type).IsRequired().HasMaxLength(30);
            builder.Property(z => z.Slots).IsRequired();

            builder.HasIndex(z => z.VillageId);
        }
    }
}