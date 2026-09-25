using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ScoutReportConfiguration : IEntityTypeConfiguration<ScoutReport>
    {
        public void Configure(EntityTypeBuilder<ScoutReport> builder)
        {
            builder.ToTable("ScoutReports");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.Id).ValueGeneratedNever();

            builder.Property(r => r.TargetName).IsRequired().HasMaxLength(100);

            // Список звітів гравця, найновіші зверху
            builder.HasIndex(r => new { r.PlayerId, r.CreatedAt });

            builder.HasMany(r => r.Resources).WithOne().HasForeignKey(x => x.ScoutReportId).OnDelete(DeleteBehavior.Cascade);
            builder.Navigation(r => r.Resources).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(r => r.DomainEvents);
        }
    }

    public class ScoutReportResourceConfiguration : IEntityTypeConfiguration<ScoutReportResource>
    {
        public void Configure(EntityTypeBuilder<ScoutReportResource> builder)
        {
            builder.ToTable("ScoutReportResources");
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedNever();

            builder.Property(x => x.ResourceType).IsRequired().HasMaxLength(50);

            builder.Ignore(x => x.DomainEvents);
        }
    }
}
