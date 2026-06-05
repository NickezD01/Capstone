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
    public class ProgressReportConfiguration : IEntityTypeConfiguration<ProgressReport>
    {
        public void Configure(EntityTypeBuilder<ProgressReport> builder)
        {
            builder.ToTable("ProgressReports");
            builder.HasKey(pr => pr.ReportId);
            builder.Property(pr => pr.SitePhotoUrl).HasMaxLength(500);

            builder.Property(pr => pr.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(pr => pr.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(pr => !pr.IsDeleted);

            // Mối quan hệ [2]: ProgressReport -> TaskItem (1-N)
            builder.HasOne(pr => pr.Task)
                   .WithMany(t => t.ProgressReports)
                   .HasForeignKey(pr => pr.TaskId)
                   .OnDelete(DeleteBehavior.Restrict);

            // Mối quan hệ [3]: ProgressReport -> UserAccount (1-N)
            builder.HasOne(pr => pr.Engineer)
                   .WithMany(u => u.ProgressReports)
                   .HasForeignKey(pr => pr.EngineerId)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
