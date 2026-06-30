using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Material
{
    public long MaterialId { get; set; }

    public string? MaterialCode { get; set; }

    public string? MaterialName { get; set; }

    public long? CategoryId { get; set; }

    public long? UnitId { get; set; }

    public string? Specification { get; set; }

    public string? Description { get; set; }

    public decimal? MinStock { get; set; }

    public string? Status { get; set; }

    public virtual MaterialCategory? Category { get; set; }

    public virtual ICollection<GoodsReceiptDetail> GoodsReceiptDetails { get; set; } = new List<GoodsReceiptDetail>();

    public virtual ICollection<MaterialInventory> MaterialInventories { get; set; } = new List<MaterialInventory>();

    public virtual ICollection<MaterialIssueDetail> MaterialIssueDetails { get; set; } = new List<MaterialIssueDetail>();

    public virtual ICollection<MaterialRequestDetail> MaterialRequestDetails { get; set; } = new List<MaterialRequestDetail>();

    public virtual ICollection<PurchaseOrderDetail> PurchaseOrderDetails { get; set; } = new List<PurchaseOrderDetail>();

    public virtual Unit? Unit { get; set; }
}
