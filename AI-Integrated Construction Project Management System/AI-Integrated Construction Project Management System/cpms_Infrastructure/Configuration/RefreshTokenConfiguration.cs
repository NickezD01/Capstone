using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Infrastructure.Configuration
{
    public class RefreshTokenConfiguration
        : IEntityTypeConfiguration<RefreshToken>
    {
        public void Configure(EntityTypeBuilder<RefreshToken> builder)
        {
            builder.ToTable("RefreshTokens");

            // PRIMARY KEY
            builder.HasKey(x => x.TokenId);

            builder.Property(x => x.TokenId)
                   .ValueGeneratedOnAdd();

            builder.Property(x => x.Token)
                   .IsRequired()
                   .HasMaxLength(64);
            builder.Property(x => x.ReplacedByTokenHash).HasMaxLength(64);
            builder.Property(x => x.ParentTokenHash).HasMaxLength(64);
            builder.Property(x => x.SessionFamilyId).IsRequired();
            builder.Property(x => x.DeviceInfo).HasMaxLength(500);

            builder.Property(x => x.ExpiresAt)
                   .IsRequired();

            builder.Property(x => x.IsRevoked)
                   .HasDefaultValue(false);

            builder.Property(x => x.CreatedAt)
                   .HasDefaultValueSql("GETUTCDATE()");

            // UNIQUE
            builder.HasIndex(x => x.Token)
                   .IsUnique();
            builder.HasIndex(x => new { x.UserId, x.SessionFamilyId });

            // RELATIONSHIP
            builder.HasOne(x => x.User)
                   .WithMany(x => x.RefreshTokens)
                   .HasForeignKey(x => x.UserId)
                   .OnDelete(DeleteBehavior.Cascade);
            builder.HasQueryFilter(x => !x.IsDeleted && !x.User.IsDeleted);
        }
    }
}
