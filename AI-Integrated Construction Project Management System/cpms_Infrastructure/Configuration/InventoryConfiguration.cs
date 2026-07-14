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

            builder.HasOne(ir => ir.Variant)
                   .WithMany(v => v.InventoryRecords)
                   .HasForeignKey(ir => ir.VariantId)
                   .OnDelete(DeleteBehavior.Restrict);

            // 🚀 ĐỒNG BỘ: Cấu hình chi tiết các trường số lượng mới theo ERD
            builder.Property(ir => ir.QuantityOnHand).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.ReservedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.OnOrderQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.ReorderLevel).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(ir => ir.UpdatedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(ir => ir.AvailableQuantity)
                   .HasColumnType("decimal(19,4)")
                   .HasComputedColumnSql("[QuantityOnHand] - [ReservedQuantity]", stored: true);
            builder.Property(ir => ir.RowVersion).IsRowVersion();

            builder.HasIndex(ir => new { ir.WarehouseId, ir.VariantId })
                   .IsUnique()
                   .HasFilter("[IsDeleted] = 0");
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_InventoryRecords_QuantityOnHand", "[QuantityOnHand] >= 0");
                t.HasCheckConstraint("CK_InventoryRecords_ReservedQuantity", "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [QuantityOnHand]");
                t.HasCheckConstraint("CK_InventoryRecords_OnOrderQuantity", "[OnOrderQuantity] >= 0");
                t.HasCheckConstraint("CK_InventoryRecords_ReorderLevel", "[ReorderLevel] >= 0");
            });

            builder.Property(ir => ir.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(ir => ir.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(ir => !ir.IsDeleted);
        }
    }
}
