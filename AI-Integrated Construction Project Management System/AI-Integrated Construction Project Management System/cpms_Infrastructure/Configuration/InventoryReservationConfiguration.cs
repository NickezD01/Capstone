using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class InventoryReservationConfiguration : IEntityTypeConfiguration<InventoryReservation>
    {
        public void Configure(EntityTypeBuilder<InventoryReservation> builder)
        {
            builder.ToTable("InventoryReservations", t =>
            {
                t.HasCheckConstraint("CK_InventoryReservations_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_InventoryReservations_Status", "[Status] IN ('ACTIVE','RELEASED','FULFILLED')");
            });
            builder.HasKey(r => r.ReservationId);
            builder.Property(r => r.Quantity).HasColumnType("decimal(18,4)");
            builder.Property(r => r.Status).IsRequired().HasMaxLength(30).HasDefaultValue(InventoryReservationStatuses.Active);
            builder.Property(r => r.ReservedAt).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(r => r.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(r => r.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(r => !r.IsDeleted);
            builder.HasIndex(r => new { r.RequestItemId, r.InventoryId }).IsUnique().HasFilter("[Status] = 'ACTIVE' AND [IsDeleted] = 0");

            builder.HasOne(r => r.InventoryRecord).WithMany(i => i.Reservations).HasForeignKey(r => r.InventoryId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(r => r.MaterialRequest).WithMany(m => m.Reservations).HasForeignKey(r => r.RequestId).OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(r => r.RequestItem).WithMany(i => i.Reservations).HasForeignKey(r => r.RequestItemId).OnDelete(DeleteBehavior.Restrict);
        }
    }
}
