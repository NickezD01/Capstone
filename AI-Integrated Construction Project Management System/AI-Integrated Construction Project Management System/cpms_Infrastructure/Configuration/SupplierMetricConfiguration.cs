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
    public class SupplierMetricConfiguration : IEntityTypeConfiguration<SupplierMetric>
    {
        public void Configure(EntityTypeBuilder<SupplierMetric> builder)
        {
            builder.ToTable("SupplierMetrics");
            builder.HasKey(sm => sm.MetricId);

            // Bắt buộc Unique trường SupplierId để thiết lập quan hệ 1:1
            builder.HasIndex(sm => sm.SupplierId).IsUnique();
            builder.Property(sm => sm.AvgDeliveryDelay).HasDefaultValue(0);
            builder.Property(sm => sm.DefectRatePct).HasDefaultValue(0);
            builder.Property(sm => sm.ReliabilityScore).HasDefaultValue(0);
            builder.Property(sm => sm.EvaluatedOrderCount).HasDefaultValue(0);
            builder.Property(sm => sm.OnTimeDeliveryRatePct).HasDefaultValue(0);
            builder.Property(sm => sm.QualityScore).HasDefaultValue(100);
            builder.Property(sm => sm.RowVersion).IsRowVersion();

            builder.ToTable(t =>
            {
                t.HasCheckConstraint("CK_SupplierMetrics_Rates", "[DefectRatePct] >= 0 AND [DefectRatePct] <= 100 AND [OnTimeDeliveryRatePct] >= 0 AND [OnTimeDeliveryRatePct] <= 100 AND [QualityScore] >= 0 AND [QualityScore] <= 100 AND [ReliabilityScore] >= 0 AND [ReliabilityScore] <= 100");
                t.HasCheckConstraint("CK_SupplierMetrics_OrderCount", "[EvaluatedOrderCount] >= 0");
            });

            builder.Property(sm => sm.CreatedDate).HasDefaultValueSql("GETUTCDATE()");
            builder.Property(sm => sm.IsDeleted).HasDefaultValue(false);
            builder.HasQueryFilter(sm => !sm.IsDeleted);

            // Mối quan hệ [6]: SupplierMetric <-> Supplier (1-1)
            builder.HasOne(sm => sm.Supplier)
                   .WithOne(s => s.SupplierMetric)
                   .HasForeignKey<SupplierMetric>(sm => sm.SupplierId)
                   .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
