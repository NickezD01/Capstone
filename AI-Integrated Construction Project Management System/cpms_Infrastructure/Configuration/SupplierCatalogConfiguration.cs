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
    public class SupplierCatalogConfiguration : IEntityTypeConfiguration<SupplierCatalog>
    {
        public void Configure(EntityTypeBuilder<SupplierCatalog> builder)
        {
            builder.ToTable("SupplierCatalogs");
            builder.HasKey(sc => sc.CatalogId);
            builder.Property(sc => sc.UnitPrice).HasColumnType("decimal(18,2)");
            builder.Property(sc => sc.MinimumOrderQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(sc => sc.SupplierSku).HasMaxLength(100);
            builder.Property(sc => sc.IsAvailable).HasDefaultValue(true);
            builder.HasIndex(sc => new { sc.SupplierId, sc.VariantId }).IsUnique().HasFilter("[IsDeleted] = 0");


            // Mối quan hệ [4]: SupplierCatalog -> Supplier (1-N)
            builder.HasOne(sc => sc.Supplier)
                   .WithMany(s => s.SupplierCatalogs)
                   .HasForeignKey(sc => sc.SupplierId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(sc => sc.Variant)
                   .WithMany(v => v.SupplierCatalogs)
                   .HasForeignKey(sc => sc.VariantId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
