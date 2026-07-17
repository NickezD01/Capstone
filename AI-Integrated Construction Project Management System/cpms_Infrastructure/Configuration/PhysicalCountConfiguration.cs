using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class PhysicalCountSessionConfiguration : IEntityTypeConfiguration<PhysicalCountSession>
{
    public void Configure(EntityTypeBuilder<PhysicalCountSession> builder)
    {
        builder.ToTable("PhysicalCountSessions", t => t.HasCheckConstraint("CK_PhysicalCountSessions_Status", "[Status] IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')"));
        builder.HasKey(x => x.SessionId);
        builder.Property(x => x.Status).HasMaxLength(30).IsRequired();
        builder.Property(x => x.StartedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.ReviewNote).HasMaxLength(1000);
        builder.Property(x => x.RowVersion).IsRowVersion();
        builder.HasIndex(x => new { x.WarehouseId, x.Status, x.StartedAt });
        builder.HasIndex(x => x.WarehouseId)
            .IsUnique()
            .HasFilter("[Status] IN ('DRAFT','PENDING_APPROVAL')");
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.CreatedByUser).WithMany().HasForeignKey(x => x.CreatedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.ReviewedByUser).WithMany().HasForeignKey(x => x.ReviewedByUserId).OnDelete(DeleteBehavior.Restrict);
    }
}

public sealed class PhysicalCountLineConfiguration : IEntityTypeConfiguration<PhysicalCountLine>
{
    public void Configure(EntityTypeBuilder<PhysicalCountLine> builder)
    {
        builder.ToTable("PhysicalCountLines", t =>
        {
            t.HasCheckConstraint("CK_PhysicalCountLines_Expected", "[ExpectedQuantity] >= 0");
            t.HasCheckConstraint("CK_PhysicalCountLines_Actual", "[ActualQuantity] IS NULL OR [ActualQuantity] >= 0");
        });
        builder.HasKey(x => x.LineId);
        builder.Ignore(x => x.VarianceQuantity);
        builder.Property(x => x.ExpectedQuantity).HasColumnType("decimal(18,4)");
        builder.Property(x => x.ExpectedInventoryRowVersion).HasColumnType("binary(8)").IsRequired();
        builder.Property(x => x.ActualQuantity).HasColumnType("decimal(18,4)");
        builder.HasIndex(x => new { x.SessionId, x.InventoryId }).IsUnique();
        builder.HasOne(x => x.Session).WithMany(x => x.Lines).HasForeignKey(x => x.SessionId).OnDelete(DeleteBehavior.Cascade);
        builder.HasOne(x => x.InventoryRecord).WithMany().HasForeignKey(x => x.InventoryId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
    }
}
