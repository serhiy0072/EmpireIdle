using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ClanStructureConfiguration : IEntityTypeConfiguration<ClanStructure>
    {
        public void Configure(EntityTypeBuilder<ClanStructure> builder)
        {
            builder.ToTable("ClanStructures");
            builder.HasKey(s => s.Id);
            builder.Property(s => s.Id).ValueGeneratedNever(); // ключ ставить домен, не БД/EF

            // Два марші прибувають одночасно — кожен зсуває час добудови
            builder.Property(s => s.Version).IsRowVersion();

            // Список споруд клану й бонус території читаються за кланом
            builder.HasIndex(s => s.ClanId);

            // Одна споруда на клітину — гонку двох закладень розв'язує індекс; він же служить карті
            builder.HasIndex(s => new { s.ServerId, s.X, s.Y }).IsUnique();

            builder.Ignore(s => s.DomainEvents);
        }
    }
}
