using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class UnitLevelUpOrderConfiguration : IEntityTypeConfiguration<UnitLevelUpOrder>
    {
        public void Configure(EntityTypeBuilder<UnitLevelUpOrder> builder)
        {
            builder.ToTable("UnitLevelUpOrders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();
            builder.Property(o => o.UnitType).IsRequired().HasMaxLength(50);
            builder.HasIndex(o => new { o.CompletesAt, o.GarrisonId });
        }
    }
}
