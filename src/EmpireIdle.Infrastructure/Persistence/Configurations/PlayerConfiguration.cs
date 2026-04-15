
using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    /// <summary>
    /// EF Core конфігурація для Player entity.
    /// </summary>
    public class PlayerConfiguration : IEntityTypeConfiguration<Player>
    {
        public void Configure(EntityTypeBuilder<Player> builder)
        {
            builder.HasKey(p => p.Id);

            builder.Property(p=>p.Username).IsRequired().HasMaxLength(50);
            builder.Property(p=>p.Email).IsRequired().HasMaxLength(200);

            builder.HasIndex(p => p.Email).IsUnique();
        }
    }
}
