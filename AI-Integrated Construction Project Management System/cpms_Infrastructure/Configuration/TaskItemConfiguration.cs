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
    public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
    {
        public void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            builder.ToTable("Tasks");
            builder.HasKey(t => t.TaskId);

            builder.Property(t => t.PhaseName).IsRequired().HasMaxLength(100);
            builder.Property(t => t.TaskName).IsRequired().HasMaxLength(200);

            // BỔ SUNG: Cấu hình kiểu dữ liệu tài chính cho AI phân tích
            builder.Property(t => t.PlannedBudget).HasColumnType("decimal(18,2)");
            builder.Property(t => t.ActualCost).HasColumnType("decimal(18,2)");

            builder.Property(t => t.Status)
                   .HasMaxLength(30)
                   .HasConversion<string>();

            builder.Property(t => t.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(t => t.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(t => !t.IsDeleted);

            // Mối quan hệ 1-N: Project -> TaskItem
            builder.HasOne(t => t.Project)
                   .WithMany(p => p.Tasks)
                   .HasForeignKey(t => t.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
