namespace cpms_Domain.Models
{
    public class MaterialVariant : Base
    {
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string VariantName { get; set; } = null!;
        // Internal stock-keeping code. Supplier-specific codes are stored in SupplierCatalog.
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Grade { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public string? Specification { get; set; }
        public string? Packaging { get; set; }
        public string Unit { get; set; } = null!;
        public bool IsActive { get; set; } = true;

        public virtual Material Material { get; set; } = null!;
        public virtual ICollection<SupplierCatalog> SupplierCatalogs { get; set; } = new List<SupplierCatalog>();
        public virtual ICollection<OrderLineItem> OrderLineItems { get; set; } = new List<OrderLineItem>();
        public virtual ICollection<MaterialRequisition> MaterialRequisitions { get; set; } = new List<MaterialRequisition>();
        public virtual ICollection<InventoryRecord> InventoryRecords { get; set; } = new List<InventoryRecord>();
        public virtual ICollection<TaskMaterialRequirement> TaskMaterialRequirements { get; set; } = new List<TaskMaterialRequirement>();
    }
}
