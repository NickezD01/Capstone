using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration;

public sealed class MaterialReturnConfiguration : IEntityTypeConfiguration<MaterialReturn>
{
    public void Configure(EntityTypeBuilder<MaterialReturn> builder)
    {
        builder.ToTable("MaterialReturns", table =>
        {
            table.HasCheckConstraint("CK_MaterialReturns_Quantity", "[Quantity] > 0");
            table.HasCheckConstraint("CK_MaterialReturns_Reason", "[ReasonCode] IN ('UNUSED','EXCESS_ISSUE','DAMAGED')");
            table.HasCheckConstraint("CK_MaterialReturns_Condition", "[Condition] IN ('USABLE','QUARANTINED')");
        });
        builder.HasKey(x => x.ReturnId);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(x => x.ReasonCode).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Condition).IsRequired().HasMaxLength(30);
        builder.Property(x => x.Note).HasMaxLength(1000);
        builder.Property(x => x.ReturnedAt).HasDefaultValueSql("GETUTCDATE()");
        builder.HasOne(x => x.MaterialRequest).WithMany().HasForeignKey(x => x.MaterialRequestId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Warehouse).WithMany().HasForeignKey(x => x.WarehouseId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.Variant).WithMany().HasForeignKey(x => x.VariantId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne(x => x.RecordedByUser).WithMany().HasForeignKey(x => x.RecordedByUserId).OnDelete(DeleteBehavior.Restrict);
        builder.HasIndex(x => new { x.MaterialRequestId, x.VariantId, x.ReturnedAt });
    }
}
