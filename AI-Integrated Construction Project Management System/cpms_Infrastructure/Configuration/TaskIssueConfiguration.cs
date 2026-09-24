using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class TaskIssueConfiguration : IEntityTypeConfiguration<TaskIssue>
    {
        public void Configure(EntityTypeBuilder<TaskIssue> builder)
        {
            builder.ToTable("TaskIssues");
            builder.HasKey(t => t.IssueId);

            builder.Property(t => t.Description).IsRequired().HasMaxLength(2000);
            builder.Property(t => t.PhotoUrl).HasMaxLength(1000);
            builder.Property(t => t.ResolutionNote).HasMaxLength(2000);
            builder.Property(t => t.Status).IsRequired().HasMaxLength(30).HasConversion<string>();
            builder.Property(t => t.RowVersion).IsRowVersion();
            builder.Property(t => t.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(t => t.IsDeleted).HasDefaultValue(false);

            builder.HasIndex(t => new { t.TaskId, t.Status });

            builder.HasQueryFilter(t => !t.IsDeleted);

            builder.HasOne(t => t.Task)
                .WithMany()
                .HasForeignKey(t => t.TaskId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(t => t.ReportedByUser)
                .WithMany()
                .HasForeignKey(t => t.ReportedByUserId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
