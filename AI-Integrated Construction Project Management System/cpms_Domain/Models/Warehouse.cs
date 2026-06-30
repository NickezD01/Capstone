using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class Warehouse
{
    public long WarehouseId { get; set; }

    public string? WarehouseName { get; set; }

    public string? Location { get; set; }

    public virtual ICollection<GoodsReceipt> GoodsReceipts { get; set; } = new List<GoodsReceipt>();

    public virtual ICollection<MaterialInventory> MaterialInventories { get; set; } = new List<MaterialInventory>();

    public virtual ICollection<MaterialIssue> MaterialIssues { get; set; } = new List<MaterialIssue>();
}
