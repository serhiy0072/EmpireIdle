using EmpireIdle.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Text;

namespace EmpireIdle.Infrastructure.Persistence.Configurations
{
    public class MonsterConfiguration : IEntityTypeConfiguration<Monster>
    {
        public void Configure(EntityTypeBuilder<Monster> builder)
        {
            builder.ToTable("Monsters");
            builder.HasKey(m => m.Id);
            builder.Property(m => m.Id).ValueGeneratedNever();

            builder.Property(m => m.Type).IsRequired().HasMaxLength(50);
            builder.HasIndex(m => m.ServerId);

            builder.Property<uint>("Version").IsRowVersion();

            builder.Ignore(m => m.DomainEvents);
        }
    }
}
