namespace cpms_Application.Response.Material
{
    public class MaterialResponse
    {
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string DefaultUnit { get; set; } = null!;
        public string? Description { get; set; }
        public bool IsActive { get; set; }
        public int CategoryId { get; set; }
        public List<MaterialVariantResponse> Variants { get; set; } = new();
    }

    public class MaterialVariantResponse
    {
        public int VariantId { get; set; }
        public int MaterialId { get; set; }
        public string MaterialName { get; set; } = null!;
        public string VariantName { get; set; } = null!;
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Grade { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public string? Specification { get; set; }
        public string? Packaging { get; set; }
        public string Unit { get; set; } = null!;
        public bool IsActive { get; set; }
    }
}
