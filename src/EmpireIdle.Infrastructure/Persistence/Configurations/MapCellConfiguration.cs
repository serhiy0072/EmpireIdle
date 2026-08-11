using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class MapCellConfiguration : IEntityTypeConfiguration<MapCell>
    {
        public void Configure(EntityTypeBuilder<MapCell> builder)
        {
            builder.ToTable("MapCells");
            builder.HasKey(c => c.Id);
            builder.Property(c => c.Id).ValueGeneratedNever();

            builder.Property(c => c.OccupantType).HasConversion<int>();

            // Одна клітина = одна позиція в межах світу
            builder.HasIndex(c => new { c.ServerId, c.X, c.Y }).IsUnique();

            // Пошук «де стоїть це село / цей монстр»
            builder.HasIndex(c => new { c.OccupantType, c.OccupantId });


            builder.Ignore(c => c.DomainEvents);
        }
    }
}
