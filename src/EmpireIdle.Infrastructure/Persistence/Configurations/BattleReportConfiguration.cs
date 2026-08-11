using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class BattleReportConfiguration : IEntityTypeConfiguration<BattleReport>
    {
        public void Configure(EntityTypeBuilder<BattleReport> builder)
        {

            builder.ToTable("BattleReports");
            builder.HasKey(r => r.Id);
            builder.Property(r => r.PlayerId).ValueGeneratedNever();

            builder.Property(r => r.TerrainType).IsRequired().HasMaxLength(30);
            builder.Property(r => r.TargetName).IsRequired().HasMaxLength(100);

            // Список звітів гравця, найновіші зверху
            builder.HasIndex(r => new { r.PlayerId, r.FoughtAt });

            builder.HasMany(r => r.Lines).WithOne().HasForeignKey(l => l.BattleReportId).OnDelete(DeleteBehavior.Cascade);

            builder.Navigation(r => r.Lines).UsePropertyAccessMode(PropertyAccessMode.Field);

            builder.Ignore(r => r.DomainEvents);
        }
    }
}
