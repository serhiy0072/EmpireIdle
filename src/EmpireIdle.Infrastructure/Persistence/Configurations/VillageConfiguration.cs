
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class VillageConfiguration : IEntityTypeConfiguration<Village>
    {
        public void Configure(EntityTypeBuilder<Village> builder)
        {
            builder.HasKey(v => v.Id);
            builder.Ignore(v => v.DomainEvents);

            builder.Property(v => v.Name).IsRequired().HasMaxLength(100);
            builder.HasMany(typeof(VillageResource),"_resources").WithOne().HasForeignKey("VillageId").IsRequired();
            builder.HasMany(v => v.Buildings).WithOne().HasForeignKey(b => b.VillageId);
        }
    }
}
