using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class ServerConfiguration : IEntityTypeConfiguration<Server>
    {
        public void Configure(EntityTypeBuilder<Server> builder)
        {
            builder.ToTable("Servers");
            builder.HasKey(s => s.Id);

            // Id задається явно при створенні світу, не базою:
            // він з'являється в токенах і в конфігах, і має бути передбачуваним
            builder.Property(s => s.Id).ValueGeneratedNever();

            builder.Property(s => s.Name).IsRequired().HasMaxLength(64);
            builder.Property(s => s.Level).IsRequired();
            builder.Property(s => s.State).HasConversion<int>().IsRequired();

            builder.Property(s => s.Version).IsRowVersion();
        }
    }
}
