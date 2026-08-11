using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class BattleReportLineConfiguration : IEntityTypeConfiguration<BattleReportLine>
    {
        public void Configure(EntityTypeBuilder<BattleReportLine> builder)
        {
            builder.ToTable("BattleReportLines");
            builder.HasKey(l => l.Id);
            builder.Property(l => l.Id).ValueGeneratedNever();

            builder.Property(l => l.UnitType).IsRequired().HasMaxLength(50);
            builder.Ignore(l => l.Survived);   // обчислювана — колонки не потрібно

            builder.Ignore(l => l.DomainEvents);
        }
    }
}
