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
    public class TaskMaterialRequirementConfiguration : IEntityTypeConfiguration<TaskMaterialRequirement>
    {
        public void Configure(EntityTypeBuilder<TaskMaterialRequirement> builder)
        {
            builder.ToTable("TaskMaterialRequirements");
            builder.HasKey(tmr => tmr.Id);

            // Cấu hình số lượng định mức (Độ chính xác cao)
            builder.Property(tmr => tmr.GrossQuantityRequired)
                   .HasColumnType("decimal(18,4)")
                   .HasDefaultValue(0.00);

            builder.Property(tmr => tmr.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(tmr => tmr.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(tmr => !tmr.IsDeleted);

            // Cấu hình mối quan hệ 1-N với TaskItem
            builder.HasOne(tmr => tmr.TaskItem)
                   .WithMany(t => t.MaterialRequirements)
                   .HasForeignKey(tmr => tmr.TaskId)
                   .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(tmr => tmr.Variant)
                   .WithMany(v => v.TaskMaterialRequirements)
                   .HasForeignKey(tmr => tmr.VariantId)
                   .OnDelete(DeleteBehavior.Restrict);

            builder.HasIndex(tmr => new { tmr.TaskId, tmr.VariantId })
                   .IsUnique()
                   .HasFilter("[IsDeleted] = 0");
        }
    }
}
