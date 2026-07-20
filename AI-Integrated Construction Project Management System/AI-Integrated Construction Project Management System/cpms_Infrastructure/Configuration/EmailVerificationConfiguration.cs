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
    public class EmailVerificationConfiguration : IEntityTypeConfiguration<EmailVerification>
    {
        public void Configure(EntityTypeBuilder<EmailVerification> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.VerificationCode)
                .IsRequired()
                .HasMaxLength(20);
            builder.Property(x => x.Purpose).IsRequired().HasMaxLength(30).HasDefaultValue(SecurityTokenPurposes.EmailVerification);
            builder.Property(x => x.FailedAttempts).HasDefaultValue(0);
            builder.HasIndex(x => new { x.UserId, x.Purpose, x.IsUsed, x.ExpiresAt });

            builder.HasOne(x => x.User)
                .WithMany(x => x.EmailVerifications)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasQueryFilter(x => !x.IsDeleted && !x.User.IsDeleted);
        }
    }
}
