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
    public class WarehouseConfiguration : IEntityTypeConfiguration<Warehouse>
    {
        public void Configure(EntityTypeBuilder<Warehouse> builder)
        {
            builder.ToTable("Warehouses");
            builder.HasKey(w => w.WarehouseId);

            builder.Property(w => w.WarehouseName).IsRequired().HasMaxLength(250);
            builder.Property(w => w.Location).HasMaxLength(500);

            builder.Property(w => w.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(w => w.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(w => !w.IsDeleted);
        }
    }
}
