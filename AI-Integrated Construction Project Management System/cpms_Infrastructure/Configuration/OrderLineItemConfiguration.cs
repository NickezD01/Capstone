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
    public class OrderLineItemConfiguration : IEntityTypeConfiguration<OrderLineItem>
    {
        public void Configure(EntityTypeBuilder<OrderLineItem> builder)
        {
            builder.ToTable("OrderLineItems");
            builder.HasKey(oli => oli.LineItemId);
            builder.Property(oli => oli.UnitPrice).HasColumnType("decimal(18,2)");

            builder.Property(oli => oli.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(oli => oli.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(oli => !oli.IsDeleted);

            // Mối quan hệ [10]: OrderLineItem -> PurchaseOrder (1-N)
            builder.HasOne(oli => oli.PurchaseOrder)
                   .WithMany(po => po.OrderLineItems)
                   .HasForeignKey(oli => oli.PoId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Mối quan hệ [11]: OrderLineItem -> Material (1-N)
            builder.HasOne(oli => oli.Material)
                   .WithMany(m => m.OrderLineItems)
                   .HasForeignKey(oli => oli.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
