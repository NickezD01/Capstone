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
            builder.Property(mreq => mreq.NeededByDate).IsRequired();

            builder.Property(mreq => mreq.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(mreq => mreq.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(mreq => !mreq.IsDeleted);

            // Quan hệ 1-N: MaterialRequest - MaterialRequisition
            builder.HasOne(mreq => mreq.MaterialRequest)
                   .WithMany(mr => mr.Requisitions)
                   .HasForeignKey(mreq => mreq.RequestId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Quan hệ 1-N: Material - MaterialRequisition
            builder.HasOne(mreq => mreq.Material)
                   .WithMany(m => m.MaterialRequisitions)
                   .HasForeignKey(mreq => mreq.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
