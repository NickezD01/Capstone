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
    public class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
    {
        public void Configure(EntityTypeBuilder<Supplier> builder)
        {
            builder.ToTable("Suppliers");
            builder.HasKey(s => s.SupplierId);

            builder.Property(s => s.CompanyName).IsRequired().HasMaxLength(200);
            builder.Property(s => s.ContactEmail).HasMaxLength(150);
            builder.Property(s => s.ContactPhone).HasMaxLength(20);
            builder.Property(s => s.Address).HasMaxLength(500);
        }
    }
}
