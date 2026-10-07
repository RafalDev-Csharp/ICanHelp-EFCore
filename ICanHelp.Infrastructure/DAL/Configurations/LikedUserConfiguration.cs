using ICanHelp.Core.Entities.Liked;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ICanHelp.Infrastructure.DAL.Configurations
{
    internal class LikedUserConfiguration : IEntityTypeConfiguration<LikedUser>
    {
        public void Configure(EntityTypeBuilder<LikedUser> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.UserLiker)
                .WithMany(x => x.LikedUsers)
                .HasForeignKey(x => x.UserLikerId)
                .OnDelete(DeleteBehavior.Cascade);
            builder.Property(x => x.IsLiked)
                .IsRequired();
            builder.HasOne(x => x.UserLiked)
                .WithMany(x => x.LikedUsers)
                .HasForeignKey(x => x.UserLikedId)
                .OnDelete(DeleteBehavior.Cascade);

        }
    }
}
