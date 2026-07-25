using cpms_Domain.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace cpms_Infrastructure.Configuration
{
    public class SystemReportConfiguration : IEntityTypeConfiguration<SystemReport>
    {
        public void Configure(EntityTypeBuilder<SystemReport> builder)
        {
            builder.HasKey(x => x.Id);
            builder.HasOne(x => x.Project)
                .WithMany(x => x.SystemReports)
                .HasForeignKey(x => x.ProjectID)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.Generator)
                .WithMany(x => x.SystemReports)
                .HasForeignKey(x => x.GeneratedBy)
                .OnDelete(DeleteBehavior.Restrict);
            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }
}
