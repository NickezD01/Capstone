using cpms_Domain.Models;
using cpms_Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;

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
        Assert.Equal("[QuantityOnHand] - [ReservedQuantity] - [QuarantineQuantity]", entity.FindProperty(nameof(InventoryRecord.AvailableQuantity))!.GetComputedColumnSql());
        Assert.Contains(entity.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[] { nameof(InventoryRecord.WarehouseId), nameof(InventoryRecord.VariantId) }));
    }

    [Fact]
    public void ProgressIncrement_UsesDecimalFiveTwo()
    {
        using var context = CreateContext();
        var property = context.Model.FindEntityType(typeof(ProgressReport))!.FindProperty(nameof(ProgressReport.ProgressIncrement))!;
        Assert.Equal("decimal(5,2)", property.GetColumnType());
        var costProperty = context.Model.FindEntityType(typeof(ProgressReport))!.FindProperty(nameof(ProgressReport.ActualCostIncrement))!;
        Assert.Equal("decimal(18,2)", costProperty.GetColumnType());
    }

    [Fact]
    public void MutableWorkflowRoots_HaveRowVersionConcurrency()
    {
        using var context = CreateContext();
        foreach (var type in new[] { typeof(TaskItem), typeof(MaterialRequest), typeof(PurchaseOrder) })
        {
            var rowVersion = context.Model.FindEntityType(type)!.FindProperty("RowVersion");
            Assert.NotNull(rowVersion);
            Assert.True(rowVersion!.IsConcurrencyToken);
        }
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

    [Fact]
    public void WarehouseTransfers_HaveConcurrencyAndUniqueTransferVariant()
    {
        using var context = CreateContext();
        var transfer = context.Model.FindEntityType(typeof(WarehouseTransfer))!;
        Assert.True(transfer.FindProperty(nameof(WarehouseTransfer.RowVersion))!.IsConcurrencyToken);

        var item = context.Model.FindEntityType(typeof(WarehouseTransferItem))!;
        Assert.Contains(item.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(WarehouseTransferItem.TransferId), nameof(WarehouseTransferItem.VariantId)
            }));
    }

    [Fact]
    public void AssignmentAndAiAlertMappings_DoNotCreateShadowForeignKeys()
    {
        using var context = CreateContext();
        var task = context.Model.FindEntityType(typeof(TaskItem))!;
        Assert.Contains(task.GetForeignKeys(), fk =>
            fk.PrincipalEntityType.ClrType == typeof(UserAccount) &&
            fk.Properties.Single().Name == nameof(TaskItem.AssignedToUserID));
        Assert.DoesNotContain(task.GetProperties(), p => p.IsShadowProperty() && p.Name.Contains("UserAccount", StringComparison.Ordinal));

        var alert = context.Model.FindEntityType(typeof(AIAlert))!;
        Assert.DoesNotContain(alert.GetProperties(), p => p.IsShadowProperty() && p.Name.Contains("UserAccount", StringComparison.Ordinal));
    }

    [Fact]
    public void AuditLedger_RemainsVisibleAndBusinessChecksAreDatabaseEnforced()
    {
        using var context = CreateContext();
        Assert.Null(context.Model.FindEntityType(typeof(InventoryTransaction))!.GetQueryFilter());

        var designModel = context.GetService<IDesignTimeModel>().Model;
        var projectChecks = designModel.FindEntityType(typeof(Project))!.GetCheckConstraints().Select(x => x.Name);
        Assert.Contains("CK_Projects_BaselineDates", projectChecks);
        var taskChecks = designModel.FindEntityType(typeof(TaskItem))!.GetCheckConstraints().Select(x => x.Name);
        Assert.Contains("CK_TaskItems_PlannedBudget", taskChecks);
        Assert.Contains("CK_TaskItems_ActualCost", taskChecks);
        Assert.Contains("CK_TaskItems_ActualProgressPct", taskChecks);
    }

    [Fact]
    public void GovernanceModels_HaveRequiredConcurrencyAndUniqueness()
    {
        using var context = CreateContext();
        Assert.True(context.Model.FindEntityType(typeof(PhysicalCountSession))!
            .FindProperty(nameof(PhysicalCountSession.RowVersion))!.IsConcurrencyToken);
        Assert.Contains(context.Model.FindEntityType(typeof(MrpPlanningRun))!.GetIndexes(), index => index.IsUnique &&
            index.Properties.Select(p => p.Name).SequenceEqual(new[]
            {
                nameof(MrpPlanningRun.ProjectId), nameof(MrpPlanningRun.WarehouseId), nameof(MrpPlanningRun.Version)
            }));
    }
}
