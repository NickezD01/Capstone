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
    public class MaterialRequestConfiguration : IEntityTypeConfiguration<MaterialRequest>
    {
        public void Configure(EntityTypeBuilder<MaterialRequest> builder)
        {
            builder.ToTable("MaterialsRequests"); // Tên bảng khớp ERD
            builder.HasKey(mr => mr.RequestId);

            builder.Property(mr => mr.Status).HasMaxLength(50).HasDefaultValue("PENDING");
            builder.Property(mr => mr.RequestDate).HasDefaultValueSql("GETUTCDATE()");

            builder.Property(mr => mr.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(mr => mr.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(mr => !mr.IsDeleted);

            // Quan hệ 1-N: Project - MaterialRequest
            builder.HasOne(mr => mr.Project)
                   .WithMany(p => p.MaterialRequests)
                   .HasForeignKey(mr => mr.ProjectId)
                   .OnDelete(DeleteBehavior.Cascade);

            // Quan hệ 1-N: UserAccount (Requester) - MaterialRequest
            builder.HasOne(mr => mr.Requester)
                   .WithMany() // Ở UserAccount không cần tạo list ngược lại
                   .HasForeignKey(mr => mr.RequestedBy)
                   .OnDelete(DeleteBehavior.Restrict);
        }
    }
}
