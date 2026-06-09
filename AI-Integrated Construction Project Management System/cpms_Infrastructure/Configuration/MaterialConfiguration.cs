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
    public class MaterialConfiguration : IEntityTypeConfiguration<Material>
    {
        public void Configure(EntityTypeBuilder<Material> builder)
        {
            builder.ToTable("Materials");
            builder.HasKey(m => m.MaterialId);

            builder.Property(m => m.MaterialName).IsRequired().HasMaxLength(200);
            builder.Property(m => m.Unit).HasMaxLength(50);

            // XÓA DÒNG NÀY: builder.Property(m => m.Category).HasMaxLength(100);

            // THÊM QUAN HỆ NÀY ĐỂ EF CORE TỰ HIỂU:
            builder.HasOne(m => m.Category) // Thuộc tính navigation trong Material
                   .WithMany(c => c.Materials) // Thuộc tính collection trong Category
                   .HasForeignKey(m => m.CategoryId) // Khóa ngoại
                   .OnDelete(DeleteBehavior.Restrict);

            builder.Property(m => m.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(m => m.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(m => !m.IsDeleted);
        }
    }
}
