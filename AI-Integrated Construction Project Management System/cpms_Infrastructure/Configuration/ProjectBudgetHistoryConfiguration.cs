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

            builder.Property(x => x.Reason)
                   .HasMaxLength(500);

            // Cấu hình mối quan hệ 1-N (Một dự án có nhiều lịch sử thay đổi ngân sách)
            builder.HasOne(x => x.Project)
                   .WithMany() // Nếu bên class Project chưa khai báo Collection này thì để trống
                   .HasForeignKey(x => x.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade); // Xóa Project thì tự xóa History
            builder.HasQueryFilter(x => !x.Project.IsDeleted);
        }
    }
}
