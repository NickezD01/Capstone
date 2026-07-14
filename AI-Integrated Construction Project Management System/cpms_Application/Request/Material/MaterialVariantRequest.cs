namespace cpms_Application.Request.Material
{
    public class MaterialVariantRequest
    {
        public int MaterialId { get; set; }
        public string VariantName { get; set; } = null!;
        public string? SKU { get; set; }
        public string? Brand { get; set; }
        public string? Grade { get; set; }
        public string? Size { get; set; }
        public string? Color { get; set; }
        public string? Specification { get; set; }
        public string? Packaging { get; set; }
        public string Unit { get; set; } = null!;
        public bool IsActive { get; set; } = true;
    }
}
