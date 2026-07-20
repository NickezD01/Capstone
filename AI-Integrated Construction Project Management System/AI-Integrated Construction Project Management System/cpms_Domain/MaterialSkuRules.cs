namespace cpms_Domain;

/// <summary>
/// Defines the canonical form of an internal material-variant SKU.
/// Supplier-specific item numbers belong to SupplierCatalog.SupplierSku.
/// </summary>
public static class MaterialSkuRules
{
    public static string? Normalize(string? sku) =>
        string.IsNullOrWhiteSpace(sku) ? null : sku.Trim().ToUpperInvariant();

    public static string Generate(int materialId, int variantId) =>
        $"MAT-{materialId:D6}-VAR-{variantId:D6}";
}
