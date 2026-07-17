using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace cpms_Domain.Models
{
    public class SupplierMetric : Base
    {
        public int MetricId { get; set; }
        public int SupplierId { get; set; }
        public double AvgDeliveryDelay { get; set; }
        public double DefectRatePct { get; set; }
        public double ReliabilityScore { get; set; }
        public int EvaluatedOrderCount { get; set; }
        public double OnTimeDeliveryRatePct { get; set; }
        public double QualityScore { get; set; }
        public DateTime? LastEvaluatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();

        // Navigation
        public virtual Supplier Supplier { get; set; } = null!;
    }
}
