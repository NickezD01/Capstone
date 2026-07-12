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
                   .HasColumnType("decimal(18,2)")
                   .HasDefaultValue(0.00);

            builder.Property(tmr => tmr.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(tmr => tmr.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(tmr => !tmr.IsDeleted);

            // Cấu hình mối quan hệ 1-N với TaskItem
            builder.HasOne(tmr => tmr.TaskItem)
                   .WithMany(t => t.MaterialRequirements)
                   .HasForeignKey(tmr => tmr.TaskId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Cấu hình mối quan hệ với Material
            builder.HasOne(tmr => tmr.Material)
                   .WithMany() // Nếu bảng Material không cần tạo ICollection ngược lại
                   .HasForeignKey(tmr => tmr.MaterialId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
