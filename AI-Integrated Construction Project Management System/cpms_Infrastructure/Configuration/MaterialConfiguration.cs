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

            // Cấu hình quan hệ với Category
            builder.HasOne(m => m.Category)
       .WithMany(c => c.Materials)
       .HasForeignKey(m => m.CategoryId)
       .OnDelete(DeleteBehavior.Restrict);

            // 💡 EF Core tự hiểu mối quan hệ 1-N với InventoryRecord thông qua cấu hình ở file InventoryRecordConfiguration, 
            // tuy nhiên bạn có thể ghi đè tường minh tại đây nếu muốn:
            builder.HasMany(m => m.Inventories)
                   .WithOne(ir => ir.Material)
                   .HasForeignKey(ir => ir.MaterialId);

            builder.Property(m => m.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(m => m.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(m => !m.IsDeleted);
        }
    }
}
