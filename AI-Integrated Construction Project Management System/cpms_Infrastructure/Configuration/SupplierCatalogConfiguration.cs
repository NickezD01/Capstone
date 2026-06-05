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


            // Mối quan hệ [4]: SupplierCatalog -> Supplier (1-N)
            builder.HasOne(sc => sc.Supplier)
                   .WithMany(s => s.SupplierCatalogs)
                   .HasForeignKey(sc => sc.SupplierId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Mối quan hệ [5]: SupplierCatalog -> Material (1-N)
            builder.HasOne(sc => sc.Material)
                   .WithMany(m => m.SupplierCatalogs)
                   .HasForeignKey(sc => sc.MaterialId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
