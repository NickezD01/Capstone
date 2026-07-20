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
    public class ProjectConfiguration : IEntityTypeConfiguration<Project>
    {
        public void Configure(EntityTypeBuilder<Project> builder)
        {
            builder.ToTable("Projects");
            builder.HasKey(p => p.ProjectId);

            builder.Property(p => p.ProjectName).IsRequired().HasMaxLength(200);
            builder.Property(p => p.Address).HasMaxLength(500);
            builder.Property(p => p.TotalProjectBudget).HasColumnType("decimal(18,2)");
            builder.Property(p => p.RowVersion).IsRowVersion();

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_Projects_BaselineDates", "[BaselineEnd] >= [BaselineStart]");
                t.HasCheckConstraint("CK_Projects_StartDate", "[StartDate] >= [BaselineStart] AND [StartDate] <= [BaselineEnd]");
                t.HasCheckConstraint("CK_Projects_TotalBudget", "[TotalProjectBudget] >= 0");
            });

            builder.Property(p => p.Status)
                   .HasMaxLength(30)
                   .HasConversion<string>();

            builder.Property(p => p.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(p => p.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(p => !p.IsDeleted);
            builder.HasOne(p => p.ProjectManager)
       .WithMany(u => u.ManagedProjects)
       .HasForeignKey(p => p.PMUserID)
       .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
