using System;
using System.Collections.Generic;

namespace cpms_Domain.Models;

public partial class MaterialInventory
{
    public long InventoryId { get; set; }

    public long? MaterialId { get; set; }

    public long? WarehouseId { get; set; }

    public decimal? Quantity { get; set; }

    public DateTime? LastUpdated { get; set; }

    public virtual Material? Material { get; set; }

    public virtual Warehouse? Warehouse { get; set; }
}
