using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class InventoryAdjustmentConfiguration : IEntityTypeConfiguration<InventoryAdjustment>
{
    public void Configure(EntityTypeBuilder<InventoryAdjustment> builder)
    {
        builder.ToTable("InventoryAdjustments", table =>
        {
            table.HasCheckConstraint("CK_InventoryAdjustments_Quantity", "[QuantityDelta] <> 0");
            table.HasCheckConstraint("CK_InventoryAdjustments_Status", "[Status] IN ('PENDING','APPROVED','REJECTED')");
            table.HasCheckConstraint("CK_InventoryAdjustments_Reason", "[ReasonCode] IN ('CYCLE_COUNT','DAMAGE','LOSS','DATA_CORRECTION','OPENING_BALANCE')");
        });
        builder.HasKey(x => x.AdjustmentId);
        builder.Property(x => x.QuantityDelta).HasColumnType("decimal(18,4)");
        builder.Property(x => x.ReasonCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.RequestedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.Status, x.WarehouseId, x.RequestedAt });
    }
}
