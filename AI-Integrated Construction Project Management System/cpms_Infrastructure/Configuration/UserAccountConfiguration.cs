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

            builder.Property(u => u.Email).IsRequired().HasMaxLength(150);
            builder.HasIndex(u => u.Email).IsUnique();
            builder.Property(u => u.FirstName).HasMaxLength(50);
            builder.Property(u => u.LastName).HasMaxLength(50);
            builder.Property(u => u.PhoneNumber).HasMaxLength(20);
            builder.Property(u => u.ImgUrl).HasMaxLength(500);
            builder.Property(u => u.PasswordHash).IsRequired();
            builder.Property(u => u.PasswordSalt).IsRequired();

            builder.Property(u => u.Role)
                   .HasMaxLength(20)
                   .HasConversion<string>();

            // Cấu hình thuộc tính từ lớp Base
            builder.Property(u => u.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(u => u.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(u => !u.IsDeleted); // Tự động lọc khi dùng Soft Delete
        }
    }
}
