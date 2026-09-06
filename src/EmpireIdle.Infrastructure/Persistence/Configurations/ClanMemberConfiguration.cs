using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanMemberConfiguration : IEntityTypeConfiguration<ClanMember>
    {
        public void Configure(EntityTypeBuilder<ClanMember> builder)
        {
            builder.ToTable("ClanMembers");
            builder.HasKey(m => m.Id);

            // Гравець в одному клані максимум. Унікальність робить це
            // структурним: перевірку в хендлері можна забути, індекс — ні
            builder.HasIndex(m => m.PlayerId).IsUnique();

            // Склад клану читається цілком і часто — індекс на власника
            builder.HasIndex(m => m.ClanId);
        }
    }
}
