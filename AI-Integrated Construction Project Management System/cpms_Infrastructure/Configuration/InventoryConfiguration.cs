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
    public class InventoryConfiguration : IEntityTypeConfiguration<Inventory>
    {
        public void Configure(EntityTypeBuilder<Inventory> builder)
        {
            builder.ToTable("Inventories");
            builder.HasKey(i => i.InventoryId);

            // Cấu hình Quan hệ 1-N (Warehouse - Inventory)
            builder.HasOne(i => i.Warehouse)
                   .WithMany(w => w.Inventories)
                   .HasForeignKey(i => i.WarehouseId)
                   .OnDelete(DeleteBehavior.Cascade); // Xóa kho sẽ xóa luôn tồn kho trong đó

            // Cấu hình Quan hệ 1-N (Material - Inventory)
            builder.HasOne(i => i.Material)
                   .WithMany(m => m.Inventories)
                   .HasForeignKey(i => i.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict); // Không cho xóa vật liệu nếu còn trong kho

            builder.Property(i => i.Quantity).HasPrecision(18, 4); // Độ chính xác cao cho số lượng

            builder.Property(i => i.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(i => i.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(i => !i.IsDeleted);
        }
    }
}
