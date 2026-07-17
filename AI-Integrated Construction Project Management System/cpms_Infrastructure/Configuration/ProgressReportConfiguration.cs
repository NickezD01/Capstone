using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;

namespace cpms_Infrastructure.Configuration
{
    public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
    {
        public void Configure(EntityTypeBuilder<ProgressReport> builder)
        {
            builder.ToTable("ProgressReports");
            builder.HasKey(pr => pr.ReportId);

            builder.Property(pr => pr.SitePhotoUrl).HasMaxLength(500);
            builder.Property(pr => pr.ProgressIncrement).HasColumnType("decimal(5,2)").HasDefaultValue(0.00); // Thêm định dạng decimal cho % tiến độ
            builder.Property(pr => pr.ActualCostIncrement).HasColumnType("decimal(18,2)").HasDefaultValue(0.00);
            builder.Property(pr => pr.Notes).HasMaxLength(2000);

            builder.Property(pr => pr.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(pr => pr.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(pr => !pr.IsDeleted);

            // 🚀 SỬA: Đổi từ pr.Task sang pr.TaskItem cho trùng khớp với thực thể ProgressReport
            builder.HasOne(pr => pr.Task)
                   .WithMany(t => t.ProgressReports)
                   .HasForeignKey(pr => pr.TaskId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Mối quan hệ [3]: ProgressReport -> UserAccount (1-N)
            builder.HasOne(pr => pr.Reporter)
                   .WithMany(u => u.ProgressReports)
                   .HasForeignKey(pr => pr.ReportedByUserId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
