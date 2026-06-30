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
    public class InventoryConfiguration : IEntityTypeConfiguration<MaterialInventory>
    {
        public void Configure(EntityTypeBuilder<MaterialInventory> builder)
        {
            builder.HasKey(i => i.InventoryId);

            // Cấu hình Quan hệ 1-N (Warehouse - Inventory)
            builder.HasOne(i => i.Warehouse)
                   .WithMany(w => w.MaterialInventories)
                   .HasForeignKey(i => i.WarehouseId);

            // Cấu hình Quan hệ 1-N (Material - Inventory)
            builder.HasOne(i => i.Material)
                   .WithMany(m => m.MaterialInventories)
                   .HasForeignKey(i => i.MaterialId);

            builder.Property(i => i.Quantity).HasPrecision(18, 4); // Độ chính xác cao cho số lượng
        }
    }
}
