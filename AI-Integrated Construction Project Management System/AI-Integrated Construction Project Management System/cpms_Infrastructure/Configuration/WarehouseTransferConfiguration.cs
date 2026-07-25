using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class WarehouseTransferConfiguration : IEntityTypeConfiguration<WarehouseTransfer>
    {
        public void Configure(EntityTypeBuilder<WarehouseTransfer> builder)
        {
            builder.ToTable("WarehouseTransfers", table =>
            {
                table.HasCheckConstraint("CK_WarehouseTransfers_DifferentWarehouses", "[SourceWarehouseId] <> [DestinationWarehouseId]");
                table.HasCheckConstraint("CK_WarehouseTransfers_Status", "[Status] IN ('REQUESTED','APPROVED','IN_TRANSIT','RECEIVED','CLOSED_WITH_VARIANCE','REJECTED','CANCELLED')");
            });
            builder.HasKey(x => x.TransferId);
            builder.Property(x => x.Status).IsRequired().HasMaxLength(30).HasDefaultValue(WarehouseTransferStatuses.Requested);
            builder.Property(x => x.Note).HasMaxLength(1000);
            builder.Property(x => x.RequestedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.Property(x => x.RowVersion).IsRowVersion();
            builder.HasQueryFilter(x => !x.IsDeleted);

            builder.HasIndex(x => new { x.SourceWarehouseId, x.Status });
            builder.HasIndex(x => new { x.DestinationWarehouseId, x.Status });
            builder.HasIndex(x => x.RequestedAt);

            builder.HasOne(x => x.SourceWarehouse).WithMany().HasForeignKey(x => x.SourceWarehouseId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.DestinationWarehouse).WithMany().HasForeignKey(x => x.DestinationWarehouseId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.RequestedByUser).WithMany().HasForeignKey(x => x.RequestedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ApprovedByUser).WithMany().HasForeignKey(x => x.ApprovedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ShippedByUser).WithMany().HasForeignKey(x => x.ShippedByUserId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.ReceivedByUser).WithMany().HasForeignKey(x => x.ReceivedByUserId).OnDelete(DeleteBehavior.Restrict);
        }
    }

    public class WarehouseTransferItemConfiguration : IEntityTypeConfiguration<WarehouseTransferItem>
    {
        public void Configure(EntityTypeBuilder<WarehouseTransferItem> builder)
        {
            builder.ToTable("WarehouseTransferItems", table =>
            {
                table.HasCheckConstraint("CK_WarehouseTransferItems_RequestedQuantity", "[RequestedQuantity] > 0");
                table.HasCheckConstraint("CK_WarehouseTransferItems_ShippedQuantity", "[ShippedQuantity] >= 0 AND [ShippedQuantity] <= [RequestedQuantity]");
                table.HasCheckConstraint("CK_WarehouseTransferItems_ReceivedQuantity", "[ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [LostQuantity] >= 0 AND [ReceivedQuantity] + [DamagedQuantity] + [LostQuantity] <= [ShippedQuantity]");
            });
            builder.HasKey(x => x.TransferItemId);
            builder.Property(x => x.RequestedQuantity).HasColumnType("decimal(18,4)");
            builder.Property(x => x.ShippedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(x => x.ReceivedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(x => x.DamagedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(x => x.LostQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(x => x.UnitCost).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(x => x.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(x => x.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(x => !x.IsDeleted);
            builder.HasIndex(x => new { x.TransferId, x.VariantId }).IsUnique().HasFilter("[IsDeleted] = 0");
            builder.HasOne(x => x.Transfer).WithMany(x => x.Items).HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Cascade);
            builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
