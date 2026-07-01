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
    public class InventoryRecordConfiguration : IEntityTypeConfiguration<InventoryRecord>
    {
        public void Configure(EntityTypeBuilder<InventoryRecord> builder)
        {
            // 🚀 ĐỒNG BỘ: Map trúng tên bảng InventoryRecords theo ERD
            builder.ToTable("InventoryRecords");
            builder.HasKey(ir => ir.InventoryId);

            // Cấu hình Quan hệ 1-N (Warehouse - InventoryRecord)
            builder.HasOne(ir => ir.Warehouse)
                   .WithMany(w => w.InventoryRecords) // Sử dụng đúng tên thuộc tính mới ở Warehouse
                   .HasForeignKey(ir => ir.WarehouseId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình Quan hệ 1-N (Material - InventoryRecord)
            builder.HasOne(ir => ir.Material)
                   .WithMany(m => m.Inventories) // Sử dụng đúng tên thuộc tính mới ở Material
                   .HasForeignKey(ir => ir.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict);

            // 🚀 ĐỒNG BỘ: Cấu hình chi tiết các trường số lượng mới theo ERD
            builder.Property(ir => ir.QuantityOnHand).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.ReservedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.ReorderLevel).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");

            builder.Property(ir => ir.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(ir => ir.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(ir => !ir.IsDeleted);
        }
    }
}
