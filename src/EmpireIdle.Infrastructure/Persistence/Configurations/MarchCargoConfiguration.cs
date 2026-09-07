using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class MarchCargoConfiguration : IEntityTypeConfiguration<MarchCargo>
    {
        public void Configure(EntityTypeBuilder<MarchCargo> builder)
        {
            builder.ToTable("MarchCargo");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Property(c => c.ResourceType).IsRequired().HasMaxLength(50);
            builder.HasIndex(c => new { c.MarchId, c.ResourceType }).IsUnique();

            builder.Ignore(c => c.DomainEvents);
        }
    }
}
