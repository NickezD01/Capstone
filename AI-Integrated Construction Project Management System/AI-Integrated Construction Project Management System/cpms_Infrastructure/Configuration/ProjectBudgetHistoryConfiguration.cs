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
    public class ProjectBudgetHistoryConfiguration : IEntityTypeConfiguration<ProjectBudgetHistory>
    {
        public void Configure(EntityTypeBuilder<ProjectBudgetHistory> builder)
        {
            builder.ToTable("ProjectBudgetHistories");

            builder.HasKey(x => x.Id);

            builder.Property(x => x.AmountChanged)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(x => x.PreviousBudget)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();

            builder.Property(x => x.NewBudget)
                   .HasColumnType("decimal(18,2)")
                   .IsRequired();
            builder.Property(x => x.RowVersion).IsRowVersion();

            builder.Property(x => x.Reason)
                   .HasMaxLength(500);

            // Cấu hình mối quan hệ 1-N (Một dự án có nhiều lịch sử thay đổi ngân sách)
            builder.HasOne(x => x.Project)
                   .WithMany(x => x.BudgetHistories)
                   .HasForeignKey(x => x.ProjectId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.HasOne(x => x.UpdatedByUser)
                   .WithMany()
                   .HasForeignKey(x => x.UpdatedByUserId)
                   .OnDelete(DeleteBehavior.Restrict);
            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_ProjectBudgetHistories_PreviousBudget", "[PreviousBudget] >= 0");
                t.HasCheckConstraint("CK_ProjectBudgetHistories_NewBudget", "[NewBudget] >= 0");
            });
        }
    }
}
