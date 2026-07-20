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
            builder.Property(oli => oli.Quantity).HasColumnType("decimal(18,4)");
            builder.Property(oli => oli.ReceivedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(oli => oli.DamagedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(oli => oli.MissingQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_OrderLineItems_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_OrderLineItems_ReceivedQuantity", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]");
                t.HasCheckConstraint("CK_OrderLineItems_DeliveryAccounting", "[DamagedQuantity] >= 0 AND [MissingQuantity] >= 0 AND [ReceivedQuantity] + [DamagedQuantity] + [MissingQuantity] <= [Quantity]");
            });

            builder.Property(oli => oli.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(oli => oli.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(oli => !oli.IsDeleted);

            // Mối quan hệ [10]: OrderLineItem -> PurchaseOrder (1-N)
            builder.HasOne(oli => oli.PurchaseOrder)
                   .WithMany(po => po.OrderLineItems)
                   .HasForeignKey(oli => oli.PoId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(oli => oli.Variant)
                   .WithMany(v => v.OrderLineItems)
                   .HasForeignKey(oli => oli.VariantId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasOne(oli => oli.RequestItem)
                   .WithMany(ri => ri.OrderLineItems)
                   .HasForeignKey(oli => oli.RequestItemId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
