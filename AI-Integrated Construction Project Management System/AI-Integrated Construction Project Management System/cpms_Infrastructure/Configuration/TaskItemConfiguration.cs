using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace cpms_Infrastructure.Configuration
{
    public class TaskItemConfiguration : IEntityTypeConfiguration<TaskItem>
    {
        public void Configure(EntityTypeBuilder<TaskItem> builder)
        {
            // 🚀 SỬA: Đồng bộ tên bảng thành TaskItems khớp với DbSet trong AppDbContext
            builder.ToTable("TaskItems");
            builder.HasKey(t => t.TaskId);

            builder.Property(t => t.PhaseName).IsRequired().HasMaxLength(100);
            builder.Property(t => t.TaskName).IsRequired().HasMaxLength(200);

            // Cấu hình kiểu dữ liệu tài chính cho AI phân tích (EVM)
            builder.Property(t => t.PlannedBudget).HasColumnType("decimal(18,2)").HasDefaultValue(0.00);
            builder.Property(t => t.ActualCost).HasColumnType("decimal(18,2)").HasDefaultValue(0.00);
            builder.Property(t => t.ActualProgressPct).HasColumnType("decimal(5,2)").HasDefaultValue(0.00);
            builder.Property(t => t.RowVersion).IsRowVersion();

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_TaskItems_PlannedBudget", "[PlannedBudget] >= 0");
                t.HasCheckConstraint("CK_TaskItems_ActualCost", "[ActualCost] >= 0");
                t.HasCheckConstraint("CK_TaskItems_ActualProgressPct", "[ActualProgressPct] >= 0 AND [ActualProgressPct] <= 100");
                t.HasCheckConstraint("CK_TaskItems_BaselineDates", "[BaselineEnd] >= [BaselineStart]");
            });

            builder.Property(t => t.Status)
                   .HasMaxLength(30)
                   .HasConversion<string>(); // Lưu Enum dưới dạng String trong DB cho dễ đọc

            builder.Property(t => t.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(t => t.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(t => !t.IsDeleted);

            // 🚀 SỬA: Đổi từ p.Tasks sang p.TaskItems cho chuẩn hóa tên thuộc tính tập hợp trong Project.cs
            builder.HasOne(t => t.Project)
                   .WithMany(p => p.Tasks)
                   .HasForeignKey(t => t.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            // 🚀 BỔ SUNG: Cấu hình khóa ngoại liên kết tới người được giao việc (AssignedToUser)
            builder.HasOne(t => t.AssignedToUser)
                   .WithMany(u => u.Tasks)
                   .HasForeignKey(t => t.AssignedToUserID)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
