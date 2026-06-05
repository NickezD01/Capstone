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
    public class PurchaseOrderConfiguration : IEntityTypeConfiguration<PurchaseOrder>
    {
        public void Configure(EntityTypeBuilder<PurchaseOrder> builder)
        {
            builder.ToTable("PurchaseOrders");
            builder.HasKey(po => po.PoId);
            builder.Property(po => po.TotalAmount).HasColumnType("decimal(18,2)");

            builder.Property(po => po.Status)
                   .HasMaxLength(30)
                   .HasConversion<string>();

            // Mối quan hệ [7]: PurchaseOrder -> Project (1-N)
            builder.HasOne(po => po.Project)
                   .WithMany(p => p.PurchaseOrders)
                   .HasForeignKey(po => po.ProjectId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Mối quan hệ [8]: PurchaseOrder -> Supplier (1-N)
            builder.HasOne(po => po.Supplier)
                   .WithMany(s => s.PurchaseOrders)
                   .HasForeignKey(po => po.SupplierId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Mối quan hệ [9]: PurchaseOrder -> UserAccount (1-N)
            builder.HasOne(po => po.UserAccount)
                   .WithMany(u => u.PurchaseOrders)
                   .HasForeignKey(po => po.UserAccountId) // Đảm bảo trùng tên FK trong Entity của bạn
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
