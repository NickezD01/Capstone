using cpms_Domain;

namespace cpms_Tests;

public class InventoryQuantityRulesTests
{
    [Fact]
    public void Reserve_RequiresPositiveQuantityWithinAvailableStock()
    {
        Assert.True(InventoryQuantityRules.CanReserve(100m, 30m, 0m, 70m));
        Assert.False(InventoryQuantityRules.CanReserve(100m, 30m, 10m, 70m));
        Assert.False(InventoryQuantityRules.CanReserve(100m, 30m, 0m, 70.0001m));
        Assert.False(InventoryQuantityRules.CanReserve(100m, 30m, 0m, 0m));
    }

    [Fact]
    public void Issue_RequiresBothPhysicalAndReservedStock()
    {
        Assert.True(InventoryQuantityRules.CanIssue(10m, 5m, 0m, 5m));
        Assert.False(InventoryQuantityRules.CanIssue(10m, 4m, 0m, 5m));
        Assert.False(InventoryQuantityRules.CanIssue(10m, 5m, 6m, 5m));
    }

    [Fact]
    public void Receipt_CannotExceedRemainingPurchaseOrderQuantity()
    {
        Assert.True(InventoryQuantityRules.CanReceive(10m, 7.5m, 2.5m));
        Assert.False(InventoryQuantityRules.CanReceive(10m, 7.5m, 2.5001m));
        Assert.False(InventoryQuantityRules.CanReceive(10m, 7.5m, 0m));
    }

    [Fact]
    public void Adjustment_CannotReduceStockBelowReservations()
    {
        Assert.True(InventoryQuantityRules.CanAdjust(10m, 4m, 0m, -6m));
        Assert.False(InventoryQuantityRules.CanAdjust(10m, 4m, 2m, -6m));
    }

    [Fact]
    public void Transfer_UsesOnlyUnreservedAvailableStock()
    {
        Assert.True(InventoryQuantityRules.CanTransfer(10m, 4m, 0m, 6m));
        Assert.False(InventoryQuantityRules.CanTransfer(10m, 4m, 2m, 6m));
        Assert.False(InventoryQuantityRules.CanTransfer(10m, 10m, 0m, 1m));
    }
}
