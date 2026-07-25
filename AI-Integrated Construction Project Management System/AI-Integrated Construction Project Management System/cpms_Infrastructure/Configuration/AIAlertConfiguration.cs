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
    public class AIAlertConfiguration : IEntityTypeConfiguration<AIAlert>
    {
        public void Configure(EntityTypeBuilder<AIAlert> builder)
        {
            builder.ToTable("AIAlerts");
            builder.HasKey(a => a.Id);

            builder.Property(a => a.AlertType).IsRequired().HasMaxLength(100);
            builder.Property(a => a.Severity).IsRequired().HasMaxLength(50); // LOW, MEDIUM, HIGH, CRITICAL
            builder.Property(a => a.Message).IsRequired().HasMaxLength(1000);

            builder.Property(a => a.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(a => a.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(a => !a.IsDeleted);

            // Quan hệ 1-N: Project - AIAlert
            builder.HasOne(a => a.Project)
                   .WithMany(p => p.AIAlerts)
                   .HasForeignKey(a => a.ProjectID)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
