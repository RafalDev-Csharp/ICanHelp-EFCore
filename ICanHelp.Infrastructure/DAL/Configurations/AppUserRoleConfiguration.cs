using ICanHelp.Core.Entities;
using ICanHelp.Core.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class AppUserRoleConfiguration :IEntityTypeConfiguration<AppUserRole>
    {
        public void Configure(EntityTypeBuilder<AppUserRole> builder)
        {
            builder.HasKey(x => x.Id);
            builder.Property(x => x.UserRoleName)
                .HasConversion(x => x.Value, x => new StringObject(x))
                .IsRequired()
                .HasMaxLength(100);
        }
    }
}
