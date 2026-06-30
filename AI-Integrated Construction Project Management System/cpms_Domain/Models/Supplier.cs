using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Supplier
{
    public long SupplierId { get; set; }

    public string? SupplierName { get; set; }

    public string? TaxCode { get; set; }

    public string? Address { get; set; }

    public string? Phone { get; set; }

    public string? Email { get; set; }

    public virtual ICollection<PurchaseOrder> PurchaseOrders { get; set; } = new List<PurchaseOrder>();
}
