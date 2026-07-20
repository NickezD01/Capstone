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
    public class UserAccountConfiguration : IEntityTypeConfiguration<UserAccount>
    {
        public void Configure(EntityTypeBuilder<UserAccount> builder)
        {
            builder.ToTable("UserAccounts");
            builder.HasKey(u => u.Id);

            // Cấu hình thuộc tính người dùng
            builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
            builder.Property(u => u.NormalizedEmail)
                   .HasMaxLength(150)
                   .HasComputedColumnSql("UPPER(LTRIM(RTRIM([Email])))", stored: true);
            builder.HasIndex(u => u.NormalizedEmail).IsUnique();
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.PasswordSalt).IsRequired();
            builder.Property(u => u.FailedLoginAttempts).HasDefaultValue(0);
            builder.Property(u => u.PasswordChangedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(u => u.RowVersion).IsRowVersion();

            builder.Property(u => u.Role)
                   .HasMaxLength(20)
                   .HasConversion<string>();

            // Thiết lập quan hệ với các bảng liên quan
            builder.HasMany(u => u.PurchaseOrders)
                   .WithOne(po => po.UserAccount)
                   .HasForeignKey(po => po.UserAccountId)
                   .OnDelete(DeleteBehavior.Restrict); // Tránh xóa User nếu PO còn tồn tại

            builder.HasMany(u => u.ProgressReports)
                   .WithOne(pr => pr.Reporter)
                   .HasForeignKey(pr => pr.ReportedByUserId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(u => u.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(u => u.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(u => !u.IsDeleted);
        }
    }
}
