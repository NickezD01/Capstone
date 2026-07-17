using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class InventoryTransactionConfiguration : IEntityTypeConfiguration<InventoryTransaction>
    {
        public void Configure(EntityTypeBuilder<InventoryTransaction> builder)
        {
            builder.ToTable("InventoryTransactions", t =>
                t.HasCheckConstraint("CK_InventoryTransactions_Type", "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT','TRANSFER_OUT','TRANSFER_IN','PHYSICAL_COUNT')"));
            builder.HasKey(t => t.TransactionId);
            builder.Property(t => t.TransactionType).IsRequired().HasMaxLength(30);
            builder.Property(t => t.Quantity).HasColumnType("decimal(18,4)");
            builder.Property(t => t.QuantityBefore).HasColumnType("decimal(18,4)");
            builder.Property(t => t.QuantityAfter).HasColumnType("decimal(18,4)");
            builder.Property(t => t.UnitCost).HasColumnType("decimal(18,4)");
            builder.Property(t => t.TotalValue).HasColumnType("decimal(38,8)");
            builder.Property(t => t.LotNumber).HasMaxLength(100);
            builder.Property(t => t.BatchNumber).HasMaxLength(100);
            builder.Property(t => t.SerialNumber).HasMaxLength(200);
            builder.Property(t => t.ReferenceType).HasMaxLength(100);
            builder.Property(t => t.Note).HasMaxLength(1000);
            builder.Property(t => t.TransactionDate).HasDefaultValueSql("GETUTCDATE()");

            builder.HasOne(t => t.InventoryRecord).WithMany(i => i.Transactions).HasForeignKey(t => t.InventoryId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(t => t.Variant).WithMany().HasForeignKey(t => t.VariantId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(t => t.Warehouse).WithMany().HasForeignKey(t => t.WarehouseId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(t => t.PerformedBy).WithMany().HasForeignKey(t => t.PerformedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasIndex(t => new { t.WarehouseId, t.VariantId, t.TransactionDate });
            builder.HasIndex(t => t.SerialNumber).IsUnique().HasFilter("[SerialNumber] IS NOT NULL");
            // Audit rows intentionally have no global query filter. Historical transactions
            // remain visible even when related operational records are soft-deleted.
        }
    }
}
