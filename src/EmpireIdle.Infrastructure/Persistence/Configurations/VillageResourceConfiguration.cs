
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF Core конфігурація для VillageResource.
    /// </summary>
    public class VillageResourceConfiguration : IEntityTypeConfiguration<VillageResource>
    {
        public void Configure(EntityTypeBuilder<VillageResource> builder)
        {
            builder.HasKey(vr => new { vr.VillageId, vr.ResourceType });

            builder.Property(vr=>vr.ResourceType).IsRequired().HasMaxLength(50);
            builder.Property(vr => vr.Amount).IsRequired();

            builder.ToTable("VillageResources");
        }
    }
}
