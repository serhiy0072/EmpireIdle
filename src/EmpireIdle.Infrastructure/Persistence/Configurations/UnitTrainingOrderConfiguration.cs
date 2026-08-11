using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class UnitTrainingOrderConfiguration : IEntityTypeConfiguration<UnitTrainingOrder>
    {
        public void Configure(EntityTypeBuilder<UnitTrainingOrder> builder)
        {
            builder.ToTable("UnitTrainingOrders");
            builder.HasKey(o => o.Id);
            builder.Property(o => o.Id).ValueGeneratedNever();
            builder.Property(o => o.UnitType).IsRequired().HasMaxLength(50);
            builder.HasIndex(o => o.CompletesAt); // сканер шукає дозрілі
        }
    }
}
