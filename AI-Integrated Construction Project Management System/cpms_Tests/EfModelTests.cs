using cpms_Domain.Models;
using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace cpms_Tests;

public class EfModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=(localdb)\\mssqllocaldb;Database=BuildSenseModelOnly;Trusted_Connection=True")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Inventory_HasConcurrencyComputedQuantityAndUniqueWarehouseVariantIndex()
    {
        using var context = CreateContext();
        var entity = context.Model.FindEntityType(typeof(InventoryRecord))!;
        Assert.True(entity.FindProperty(nameof(InventoryRecord.RowVersion))!.IsConcurrencyToken);
        Assert.Equal("[QuantityOnHand] - [ReservedQuantity]", entity.FindProperty(nameof(InventoryRecord.AvailableQuantity))!.GetComputedColumnSql());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(InventoryRecord.WarehouseId), nameof(InventoryRecord.VariantId) }));
    }

    [Fact]
    public void ProgressIncrement_UsesDecimalFiveTwo()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(ProgressReport))!.FindProperty(nameof(ProgressReport.ProgressIncrement))!;
        Assert.Equal("decimal(5,2)", property.GetColumnType());
    }

    [Fact]
    public void OperationalEntities_ReferenceVariantsInsteadOfMaterials()
    {
        using var context = CreateContext();
        var types = new[] { typeof(InventoryRecord), typeof(MaterialRequisition), typeof(OrderLineItem), typeof(SupplierCatalog), typeof(TaskMaterialRequirement) };
        foreach (var type in types)
        {
            var entity = context.Model.FindEntityType(type)!;
            Assert.NotNull(entity.FindProperty("VariantId"));
            Assert.Null(entity.FindProperty("MaterialId"));
        }
    }
}
