using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanRoleConfiguration : IEntityTypeConfiguration<ClanRole>
    {
        public void Configure(EntityTypeBuilder<ClanRole> builder)
        {
            builder.ToTable("ClanRoles");
            builder.HasKey(r => r.Id);

            builder.Property(r => r.Name).IsRequired().HasMaxLength(32);
            builder.Property(r => r.Permissions).HasConversion<int>();

            // Назви ролей унікальні в межах клану — інакше в списку
            // два «Офіцери», і незрозуміло, кого призначаєш
            builder.HasIndex(r => new { r.ClanId, r.Name }).IsUnique();
        }
    }
}
