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
    public class MaterialRequisitionConfiguration : IEntityTypeConfiguration<MaterialRequisition>
    {
        public void Configure(EntityTypeBuilder<MaterialRequisition> builder)
        {
            builder.ToTable("MaterialsRequisitions");
            builder.HasKey(mreq => mreq.ItemId);

            builder.Property(mreq => mreq.Quantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(mreq => mreq.ApprovedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(mreq => mreq.IssuedQuantity).HasColumnType("decimal(18,4)").HasDefaultValue(0);
            builder.Property(mreq => mreq.NeededByDate).IsRequired();
            builder.Property(mreq => mreq.Note).HasMaxLength(1000);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_MaterialsRequisitions_Quantity", "[Quantity] > 0");
                t.HasCheckConstraint("CK_MaterialsRequisitions_ApprovedQuantity", "[ApprovedQuantity] >= 0 AND [ApprovedQuantity] <= [Quantity]");
                t.HasCheckConstraint("CK_MaterialsRequisitions_IssuedQuantity", "[IssuedQuantity] >= 0 AND [IssuedQuantity] <= [ApprovedQuantity]");
            });

            builder.Property(mreq => mreq.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(mreq => mreq.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(mreq => !mreq.IsDeleted);

            // Quan hệ 1-N: MaterialRequest - MaterialRequisition
            builder.HasOne(mreq => mreq.MaterialRequest)
                   .WithMany(mr => mr.Requisitions)
                   .HasForeignKey(mreq => mreq.RequestId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(mreq => mreq.Variant)
                   .WithMany(v => v.MaterialRequisitions)
                   .HasForeignKey(mreq => mreq.VariantId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
