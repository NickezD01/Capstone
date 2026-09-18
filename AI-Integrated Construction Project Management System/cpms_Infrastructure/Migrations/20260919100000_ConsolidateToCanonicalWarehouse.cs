using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations;

public partial class ConsolidateToCanonicalWarehouse : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DECLARE @CanonicalWarehouseId int = (
                SELECT MIN([WarehouseId]) FROM [Warehouses] WHERE [IsDeleted] = 0);

            IF @CanonicalWarehouseId IS NULL
                THROW 51001, 'Cannot consolidate warehouses because no active warehouse exists.', 1;

            SELECT
                [VariantId],
                SUM([QuantityOnHand]) AS [QuantityOnHand],
                SUM([ReservedQuantity]) AS [ReservedQuantity],
                SUM([OnOrderQuantity]) AS [OnOrderQuantity],
                MAX([ReorderLevel]) AS [ReorderLevel],
                SUM([QuarantineQuantity]) AS [QuarantineQuantity],
                CASE WHEN SUM([QuantityOnHand]) > 0
                    THEN SUM([QuantityOnHand] * [AverageUnitCost]) / SUM([QuantityOnHand])
                    ELSE MAX([AverageUnitCost]) END AS [AverageUnitCost],
                MAX([UpdatedAt]) AS [UpdatedAt]
            INTO #ConsolidatedInventory
            FROM [InventoryRecords]
            WHERE [IsDeleted] = 0
            GROUP BY [VariantId];

            MERGE [InventoryRecords] AS target
            USING #ConsolidatedInventory AS source
                ON target.[WarehouseId] = @CanonicalWarehouseId
               AND target.[VariantId] = source.[VariantId]
               AND target.[IsDeleted] = 0
            WHEN MATCHED THEN UPDATE SET
                [QuantityOnHand] = source.[QuantityOnHand],
                [ReservedQuantity] = source.[ReservedQuantity],
                [OnOrderQuantity] = source.[OnOrderQuantity],
                [ReorderLevel] = source.[ReorderLevel],
                [QuarantineQuantity] = source.[QuarantineQuantity],
                [AverageUnitCost] = source.[AverageUnitCost],
                [UpdatedAt] = source.[UpdatedAt]
            WHEN NOT MATCHED THEN INSERT
                ([WarehouseId], [VariantId], [QuantityOnHand], [ReservedQuantity], [OnOrderQuantity],
                 [ReorderLevel], [QuarantineQuantity], [AverageUnitCost], [UpdatedAt], [CreatedDate], [IsDeleted])
            VALUES
                (@CanonicalWarehouseId, source.[VariantId], source.[QuantityOnHand], source.[ReservedQuantity],
                 source.[OnOrderQuantity], source.[ReorderLevel], source.[QuarantineQuantity], source.[AverageUnitCost],
                 source.[UpdatedAt], GETUTCDATE(), 0);

            UPDATE reservation
            SET [InventoryId] = canonical.[InventoryId]
            FROM [InventoryReservations] AS reservation
            INNER JOIN [InventoryRecords] AS oldInventory
                ON oldInventory.[InventoryId] = reservation.[InventoryId]
            INNER JOIN [InventoryRecords] AS canonical
                ON canonical.[WarehouseId] = @CanonicalWarehouseId
               AND canonical.[VariantId] = oldInventory.[VariantId]
               AND canonical.[IsDeleted] = 0
            WHERE oldInventory.[WarehouseId] <> @CanonicalWarehouseId
              AND reservation.[Status] = 'ACTIVE';

            UPDATE [MaterialRequests]
            SET [WarehouseId] = @CanonicalWarehouseId
            WHERE [IsDeleted] = 0;

            UPDATE [PurchaseOrders]
            SET [WarehouseId] = @CanonicalWarehouseId
            WHERE [IsDeleted] = 0;

            UPDATE [InventoryAdjustments]
            SET [WarehouseId] = @CanonicalWarehouseId
            WHERE [IsDeleted] = 0
              AND [Status] = 'PENDING';

            UPDATE [PhysicalCountSessions]
            SET [WarehouseId] = @CanonicalWarehouseId
            WHERE [IsDeleted] = 0
              AND [Status] IN ('DRAFT', 'PENDING_APPROVAL');

            UPDATE [InventoryRecords]
            SET [IsDeleted] = 1,
                [ModifiedDate] = GETUTCDATE()
            WHERE [IsDeleted] = 0
              AND [WarehouseId] <> @CanonicalWarehouseId;

            UPDATE [Warehouses]
            SET [IsDeleted] = 1,
                [ModifiedDate] = GETUTCDATE()
            WHERE [IsDeleted] = 0
              AND [WarehouseId] <> @CanonicalWarehouseId;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        throw new NotSupportedException("Warehouse consolidation is intentionally irreversible because it merges inventory quantities.");
    }
}
