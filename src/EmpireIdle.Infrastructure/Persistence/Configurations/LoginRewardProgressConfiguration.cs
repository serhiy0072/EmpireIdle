using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations;

public class LoginRewardProgressConfiguration : IEntityTypeConfiguration<LoginRewardProgress>
{
    public void Configure(EntityTypeBuilder<LoginRewardProgress> builder)
    {
        builder.ToTable("LoginRewardProgress");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        // Перший вхід із двох вкладок: другий рядок не пройде
        builder.HasIndex(p => p.PlayerId).IsUnique();

        // Два входи одночасно видали б день двічі
        builder.Property(p => p.Version).IsRowVersion();

        builder.Ignore(p => p.DomainEvents);
    }
}
