using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class TransferInventoryReservationConfiguration : IEntityTypeConfiguration<TransferInventoryReservation>
{
    public void Configure(EntityTypeBuilder<TransferInventoryReservation> builder)
    {
        builder.ToTable("TransferInventoryReservations", table =>
        {
            table.HasCheckConstraint("CK_TransferInventoryReservations_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_TransferInventoryReservations_Status", "[Status] IN ('ACTIVE','CONSUMED','RELEASED')");
        });
        builder.HasKey(x => x.TransferReservationId);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(x => x.Status).IsRequired().HasMaxLength(20);
        builder.Property(x => x.CreatedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.HasIndex(x => x.TransferItemId).IsUnique();
        builder.HasOne(x => x.Transfer).WithMany().HasForeignKey(x => x.TransferId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.TransferItem).WithMany().HasForeignKey(x => x.TransferItemId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Inventory).WithMany().HasForeignKey(x => x.InventoryId).OnDelete(DeleteBehavior.Restrict);
    }
}
