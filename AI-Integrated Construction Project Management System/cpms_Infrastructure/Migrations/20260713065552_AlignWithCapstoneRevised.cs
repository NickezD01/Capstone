using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignWithCapstoneRevised : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT 1 FROM InventoryRecords WHERE IsDeleted = 0
                    GROUP BY WarehouseId, MaterialId HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot migrate: duplicate active InventoryRecords exist for WarehouseId and MaterialId.', 1;

                IF EXISTS (
                    SELECT 1 FROM TaskMaterialRequirements WHERE IsDeleted = 0
                    GROUP BY TaskId, MaterialId HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot migrate: duplicate active TaskMaterialRequirements exist for TaskId and MaterialId.', 1;

                IF EXISTS (
                    SELECT 1 FROM SupplierCatalogs WHERE IsDeleted = 0
                    GROUP BY SupplierId, MaterialId HAVING COUNT(*) > 1)
                    THROW 51000, 'Cannot migrate: duplicate active SupplierCatalogs exist for SupplierId and MaterialId.', 1;

                IF EXISTS (SELECT 1 FROM PurchaseOrders)
                   AND NOT EXISTS (SELECT 1 FROM Warehouses WHERE IsDeleted = 0)
                    THROW 51000, 'Cannot migrate purchase orders because no active warehouse exists.', 1;

                IF EXISTS (SELECT 1 FROM MaterialsRequisitions WHERE Quantity <= 0)
                    THROW 51000, 'Cannot migrate: MaterialsRequisitions contains a non-positive quantity.', 1;

                IF EXISTS (SELECT 1 FROM OrderLineItems WHERE Quantity <= 0)
                    THROW 51000, 'Cannot migrate: OrderLineItems contains a non-positive quantity.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryRecords_Materials_MaterialId",
                table: "InventoryRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequisitions_Materials_MaterialId",
                table: "MaterialsRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLineItems_Materials_MaterialId",
                table: "OrderLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressReports_UserAccounts_EngineerId",
                table: "ProgressReports");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCatalogs_Materials_MaterialId",
                table: "SupplierCatalogs");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskMaterialRequirements_Materials_MaterialId",
                table: "TaskMaterialRequirements");

            migrationBuilder.DropIndex(
                name: "IX_TaskMaterialRequirements_TaskId",
                table: "TaskMaterialRequirements");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCatalogs_SupplierId",
                table: "SupplierCatalogs");

            migrationBuilder.DropIndex(
                name: "IX_InventoryRecords_WarehouseId",
                table: "InventoryRecords");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "TaskMaterialRequirements",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskMaterialRequirements_MaterialId",
                table: "TaskMaterialRequirements",
                newName: "IX_TaskMaterialRequirements_VariantId");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "SupplierCatalogs",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierCatalogs_MaterialId",
                table: "SupplierCatalogs",
                newName: "IX_SupplierCatalogs_VariantId");

            migrationBuilder.RenameColumn(
                name: "DeliveryDate",
                table: "PurchaseOrders",
                newName: "ExpectedDeliveryDate");

            migrationBuilder.RenameColumn(
                name: "EngineerId",
                table: "ProgressReports",
                newName: "ReportedByUserId");

            migrationBuilder.RenameIndex(
                name: "IX_ProgressReports_EngineerId",
                table: "ProgressReports",
                newName: "IX_ProgressReports_ReportedByUserId");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "OrderLineItems",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_OrderLineItems_MaterialId",
                table: "OrderLineItems",
                newName: "IX_OrderLineItems_VariantId");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "MaterialsRequisitions",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_MaterialsRequisitions_MaterialId",
                table: "MaterialsRequisitions",
                newName: "IX_MaterialsRequisitions_VariantId");

            migrationBuilder.RenameColumn(
                name: "Unit",
                table: "Materials",
                newName: "DefaultUnit");

            migrationBuilder.RenameColumn(
                name: "MaterialId",
                table: "InventoryRecords",
                newName: "VariantId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryRecords_MaterialId",
                table: "InventoryRecords",
                newName: "IX_InventoryRecords_VariantId");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossQuantityRequired",
                table: "TaskMaterialRequirements",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)",
                oldDefaultValue: 0m);

            migrationBuilder.AddColumn<bool>(
                name: "IsAvailable",
                table: "SupplierCatalogs",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MinimumOrderQuantity",
                table: "SupplierCatalogs",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "SupplierSku",
                table: "SupplierCatalogs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "PurchaseOrders",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "PurchaseOrders",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "PurchaseOrders",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "OrderLineItems",
                type: "decimal(18,4)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            migrationBuilder.AddColumn<decimal>(
                name: "ReceivedQuantity",
                table: "OrderLineItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "RequestItemId",
                table: "OrderLineItems",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ApprovedQuantity",
                table: "MaterialsRequisitions",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "IssuedQuantity",
                table: "MaterialsRequisitions",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "MaterialsRequisitions",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ApprovedAt",
                table: "MaterialsRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ApprovedByUserId",
                table: "MaterialsRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DecisionNote",
                table: "MaterialsRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestNote",
                table: "MaterialsRequests",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "WarehouseId",
                table: "MaterialsRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "Materials",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Materials",
                type: "bit",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<decimal>(
                name: "OnOrderQuantity",
                table: "InventoryRecords",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "InventoryRecords",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<decimal>(
                name: "AvailableQuantity",
                table: "InventoryRecords",
                type: "decimal(19,4)",
                nullable: false,
                computedColumnSql: "[QuantityOnHand] - [ReservedQuantity]",
                stored: true);

            migrationBuilder.CreateTable(
                name: "InventoryReservations",
                columns: table => new
                {
                    ReservationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    RequestItemId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "ACTIVE"),
                    ReservedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReleasedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    FulfilledAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryReservations", x => x.ReservationId);
                    table.CheckConstraint("CK_InventoryReservations_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_InventoryReservations_Status", "[Status] IN ('ACTIVE','RELEASED','FULFILLED')");
                    table.ForeignKey(
                        name: "FK_InventoryReservations_InventoryRecords_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "InventoryRecords",
                        principalColumn: "InventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_MaterialsRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MaterialsRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryReservations_MaterialsRequisitions_RequestItemId",
                        column: x => x.RequestItemId,
                        principalTable: "MaterialsRequisitions",
                        principalColumn: "ItemId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialVariants",
                columns: table => new
                {
                    VariantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialId = table.Column<int>(type: "int", nullable: false),
                    VariantName = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: false),
                    SKU = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Brand = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Grade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Size = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Color = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Specification = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Packaging = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialVariants", x => x.VariantId);
                    table.ForeignKey(
                        name: "FK_MaterialVariants_Materials_MaterialId",
                        column: x => x.MaterialId,
                        principalTable: "Materials",
                        principalColumn: "MaterialId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.Sql("""
                INSERT INTO MaterialVariants
                    (MaterialId, VariantName, SKU, Unit, IsActive, CreatedDate, IsDeleted)
                SELECT
                    MaterialId,
                    LEFT(CONCAT(MaterialName, ' Default'), 250),
                    CONCAT('LEGACY-', MaterialId),
                    DefaultUnit,
                    1,
                    COALESCE(CreatedDate, GETUTCDATE()),
                    0
                FROM Materials;

                UPDATE target SET VariantId = variant.VariantId
                FROM InventoryRecords AS target
                INNER JOIN MaterialVariants AS variant ON variant.MaterialId = target.VariantId;

                UPDATE target SET VariantId = variant.VariantId
                FROM MaterialsRequisitions AS target
                INNER JOIN MaterialVariants AS variant ON variant.MaterialId = target.VariantId;

                UPDATE target SET VariantId = variant.VariantId
                FROM OrderLineItems AS target
                INNER JOIN MaterialVariants AS variant ON variant.MaterialId = target.VariantId;

                UPDATE target SET VariantId = variant.VariantId
                FROM SupplierCatalogs AS target
                INNER JOIN MaterialVariants AS variant ON variant.MaterialId = target.VariantId;

                UPDATE target SET VariantId = variant.VariantId
                FROM TaskMaterialRequirements AS target
                INNER JOIN MaterialVariants AS variant ON variant.MaterialId = target.VariantId;

                DECLARE @LegacyWarehouseId int = (
                    SELECT MIN(WarehouseId) FROM Warehouses WHERE IsDeleted = 0);
                UPDATE PurchaseOrders SET WarehouseId = @LegacyWarehouseId;

                UPDATE MaterialsRequisitions
                SET ApprovedQuantity = Quantity,
                    IssuedQuantity = Quantity
                WHERE RequestId IN (
                    SELECT RequestId FROM MaterialsRequests WHERE Status = 'APPROVED');

                UPDATE MaterialsRequests
                SET Status = 'ISSUED'
                WHERE Status = 'APPROVED';

                UPDATE InventoryRecords
                SET ReservedQuantity = 0,
                    UpdatedAt = GETUTCDATE();

                UPDATE line
                SET ReceivedQuantity = line.Quantity
                FROM OrderLineItems AS line
                INNER JOIN PurchaseOrders AS po ON po.PoId = line.PoId
                WHERE po.Status = 'DELIVERED';

                INSERT INTO InventoryRecords
                    (WarehouseId, VariantId, QuantityOnHand, ReservedQuantity, OnOrderQuantity,
                     ReorderLevel, UpdatedAt, CreatedDate, IsDeleted)
                SELECT DISTINCT
                    po.WarehouseId, line.VariantId, 0, 0, 0, 0,
                    GETUTCDATE(), GETUTCDATE(), 0
                FROM PurchaseOrders AS po
                INNER JOIN OrderLineItems AS line ON line.PoId = po.PoId
                WHERE po.Status = 'APPROVED'
                  AND NOT EXISTS (
                      SELECT 1 FROM InventoryRecords AS inventory
                      WHERE inventory.WarehouseId = po.WarehouseId
                        AND inventory.VariantId = line.VariantId
                        AND inventory.IsDeleted = 0);

                UPDATE inventory
                SET OnOrderQuantity = orders.RemainingQuantity,
                    UpdatedAt = GETUTCDATE()
                FROM InventoryRecords AS inventory
                INNER JOIN (
                    SELECT po.WarehouseId, line.VariantId,
                           SUM(line.Quantity - line.ReceivedQuantity) AS RemainingQuantity
                    FROM PurchaseOrders AS po
                    INNER JOIN OrderLineItems AS line ON line.PoId = po.PoId
                    WHERE po.Status = 'APPROVED'
                    GROUP BY po.WarehouseId, line.VariantId
                ) AS orders
                    ON orders.WarehouseId = inventory.WarehouseId
                   AND orders.VariantId = inventory.VariantId;
                """);

            migrationBuilder.CreateTable(
                name: "InventoryTransactions",
                columns: table => new
                {
                    TransactionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityBefore = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    QuantityAfter = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReferenceId = table.Column<int>(type: "int", nullable: true),
                    ReferenceType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: false),
                    TransactionDate = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryTransactions", x => x.TransactionId);
                    table.CheckConstraint("CK_InventoryTransactions_Type", "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT')");
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_InventoryRecords_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "InventoryRecords",
                        principalColumn: "InventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_MaterialVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "MaterialVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_UserAccounts_PerformedByUserId",
                        column: x => x.PerformedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryTransactions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TaskMaterialRequirements_TaskId_VariantId",
                table: "TaskMaterialRequirements",
                columns: new[] { "TaskId", "VariantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCatalogs_SupplierId_VariantId",
                table: "SupplierCatalogs",
                columns: new[] { "SupplierId", "VariantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_ApprovedByUserId",
                table: "PurchaseOrders",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_WarehouseId",
                table: "PurchaseOrders",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_OrderLineItems_RequestItemId",
                table: "OrderLineItems",
                column: "RequestItemId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLineItems_Quantity",
                table: "OrderLineItems",
                sql: "[Quantity] > 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLineItems_ReceivedQuantity",
                table: "OrderLineItems",
                sql: "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequisitions_ApprovedQuantity",
                table: "MaterialsRequisitions",
                sql: "[ApprovedQuantity] >= 0 AND [ApprovedQuantity] <= [Quantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequisitions_IssuedQuantity",
                table: "MaterialsRequisitions",
                sql: "[IssuedQuantity] >= 0 AND [IssuedQuantity] <= [ApprovedQuantity]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequisitions_Quantity",
                table: "MaterialsRequisitions",
                sql: "[Quantity] > 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialsRequests_ApprovedByUserId",
                table: "MaterialsRequests",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialsRequests_WarehouseId",
                table: "MaterialsRequests",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_WarehouseId_VariantId",
                table: "InventoryRecords",
                columns: new[] { "WarehouseId", "VariantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_OnOrderQuantity",
                table: "InventoryRecords",
                sql: "[OnOrderQuantity] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_QuantityOnHand",
                table: "InventoryRecords",
                sql: "[QuantityOnHand] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_ReorderLevel",
                table: "InventoryRecords",
                sql: "[ReorderLevel] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords",
                sql: "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [QuantityOnHand]");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_InventoryId",
                table: "InventoryReservations",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_RequestId",
                table: "InventoryReservations",
                column: "RequestId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryReservations_RequestItemId_InventoryId",
                table: "InventoryReservations",
                columns: new[] { "RequestItemId", "InventoryId" },
                unique: true,
                filter: "[Status] = 'ACTIVE' AND [IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_InventoryId",
                table: "InventoryTransactions",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_PerformedByUserId",
                table: "InventoryTransactions",
                column: "PerformedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_VariantId",
                table: "InventoryTransactions",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_WarehouseId_VariantId_TransactionDate",
                table: "InventoryTransactions",
                columns: new[] { "WarehouseId", "VariantId", "TransactionDate" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialVariants_MaterialId",
                table: "MaterialVariants",
                column: "MaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialVariants_SKU",
                table: "MaterialVariants",
                column: "SKU",
                unique: true,
                filter: "[SKU] IS NOT NULL AND [IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryRecords_MaterialVariants_VariantId",
                table: "InventoryRecords",
                column: "VariantId",
                principalTable: "MaterialVariants",
                principalColumn: "VariantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequests_UserAccounts_ApprovedByUserId",
                table: "MaterialsRequests",
                column: "ApprovedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequests_Warehouses_WarehouseId",
                table: "MaterialsRequests",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "WarehouseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequisitions_MaterialVariants_VariantId",
                table: "MaterialsRequisitions",
                column: "VariantId",
                principalTable: "MaterialVariants",
                principalColumn: "VariantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLineItems_MaterialVariants_VariantId",
                table: "OrderLineItems",
                column: "VariantId",
                principalTable: "MaterialVariants",
                principalColumn: "VariantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLineItems_MaterialsRequisitions_RequestItemId",
                table: "OrderLineItems",
                column: "RequestItemId",
                principalTable: "MaterialsRequisitions",
                principalColumn: "ItemId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressReports_UserAccounts_ReportedByUserId",
                table: "ProgressReports",
                column: "ReportedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_UserAccounts_ApprovedByUserId",
                table: "PurchaseOrders",
                column: "ApprovedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Warehouses_WarehouseId",
                table: "PurchaseOrders",
                column: "WarehouseId",
                principalTable: "Warehouses",
                principalColumn: "WarehouseId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCatalogs_MaterialVariants_VariantId",
                table: "SupplierCatalogs",
                column: "VariantId",
                principalTable: "MaterialVariants",
                principalColumn: "VariantId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskMaterialRequirements_MaterialVariants_VariantId",
                table: "TaskMaterialRequirements",
                column: "VariantId",
                principalTable: "MaterialVariants",
                principalColumn: "VariantId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_InventoryRecords_MaterialVariants_VariantId",
                table: "InventoryRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequests_UserAccounts_ApprovedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequests_Warehouses_WarehouseId",
                table: "MaterialsRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequisitions_MaterialVariants_VariantId",
                table: "MaterialsRequisitions");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLineItems_MaterialVariants_VariantId",
                table: "OrderLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_OrderLineItems_MaterialsRequisitions_RequestItemId",
                table: "OrderLineItems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressReports_UserAccounts_ReportedByUserId",
                table: "ProgressReports");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_UserAccounts_ApprovedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Warehouses_WarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_SupplierCatalogs_MaterialVariants_VariantId",
                table: "SupplierCatalogs");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskMaterialRequirements_MaterialVariants_VariantId",
                table: "TaskMaterialRequirements");

            migrationBuilder.DropTable(
                name: "InventoryReservations");

            migrationBuilder.DropTable(
                name: "InventoryTransactions");

            migrationBuilder.Sql("""
                UPDATE target SET VariantId = variant.MaterialId
                FROM InventoryRecords AS target
                INNER JOIN MaterialVariants AS variant ON variant.VariantId = target.VariantId;

                UPDATE target SET VariantId = variant.MaterialId
                FROM MaterialsRequisitions AS target
                INNER JOIN MaterialVariants AS variant ON variant.VariantId = target.VariantId;

                UPDATE target SET VariantId = variant.MaterialId
                FROM OrderLineItems AS target
                INNER JOIN MaterialVariants AS variant ON variant.VariantId = target.VariantId;

                UPDATE target SET VariantId = variant.MaterialId
                FROM SupplierCatalogs AS target
                INNER JOIN MaterialVariants AS variant ON variant.VariantId = target.VariantId;

                UPDATE target SET VariantId = variant.MaterialId
                FROM TaskMaterialRequirements AS target
                INNER JOIN MaterialVariants AS variant ON variant.VariantId = target.VariantId;
                """);

            migrationBuilder.DropTable(
                name: "MaterialVariants");

            migrationBuilder.DropIndex(
                name: "IX_TaskMaterialRequirements_TaskId_VariantId",
                table: "TaskMaterialRequirements");

            migrationBuilder.DropIndex(
                name: "IX_SupplierCatalogs_SupplierId_VariantId",
                table: "SupplierCatalogs");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_ApprovedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_WarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_OrderLineItems_RequestItemId",
                table: "OrderLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLineItems_Quantity",
                table: "OrderLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLineItems_ReceivedQuantity",
                table: "OrderLineItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequisitions_ApprovedQuantity",
                table: "MaterialsRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequisitions_IssuedQuantity",
                table: "MaterialsRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequisitions_Quantity",
                table: "MaterialsRequisitions");

            migrationBuilder.DropIndex(
                name: "IX_MaterialsRequests_ApprovedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialsRequests_WarehouseId",
                table: "MaterialsRequests");

            migrationBuilder.DropIndex(
                name: "IX_InventoryRecords_WarehouseId_VariantId",
                table: "InventoryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_OnOrderQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_QuantityOnHand",
                table: "InventoryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_ReorderLevel",
                table: "InventoryRecords");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "AvailableQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "IsAvailable",
                table: "SupplierCatalogs");

            migrationBuilder.DropColumn(
                name: "MinimumOrderQuantity",
                table: "SupplierCatalogs");

            migrationBuilder.DropColumn(
                name: "SupplierSku",
                table: "SupplierCatalogs");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "ReceivedQuantity",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "RequestItemId",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "ApprovedQuantity",
                table: "MaterialsRequisitions");

            migrationBuilder.DropColumn(
                name: "IssuedQuantity",
                table: "MaterialsRequisitions");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "MaterialsRequisitions");

            migrationBuilder.DropColumn(
                name: "ApprovedAt",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "ApprovedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "DecisionNote",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "RequestNote",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "WarehouseId",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Materials");

            migrationBuilder.DropColumn(
                name: "OnOrderQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "InventoryRecords");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "TaskMaterialRequirements",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_TaskMaterialRequirements_VariantId",
                table: "TaskMaterialRequirements",
                newName: "IX_TaskMaterialRequirements_MaterialId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "SupplierCatalogs",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_SupplierCatalogs_VariantId",
                table: "SupplierCatalogs",
                newName: "IX_SupplierCatalogs_MaterialId");

            migrationBuilder.RenameColumn(
                name: "ExpectedDeliveryDate",
                table: "PurchaseOrders",
                newName: "DeliveryDate");

            migrationBuilder.RenameColumn(
                name: "ReportedByUserId",
                table: "ProgressReports",
                newName: "EngineerId");

            migrationBuilder.RenameIndex(
                name: "IX_ProgressReports_ReportedByUserId",
                table: "ProgressReports",
                newName: "IX_ProgressReports_EngineerId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "OrderLineItems",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_OrderLineItems_VariantId",
                table: "OrderLineItems",
                newName: "IX_OrderLineItems_MaterialId");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "MaterialsRequisitions",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_MaterialsRequisitions_VariantId",
                table: "MaterialsRequisitions",
                newName: "IX_MaterialsRequisitions_MaterialId");

            migrationBuilder.RenameColumn(
                name: "DefaultUnit",
                table: "Materials",
                newName: "Unit");

            migrationBuilder.RenameColumn(
                name: "VariantId",
                table: "InventoryRecords",
                newName: "MaterialId");

            migrationBuilder.RenameIndex(
                name: "IX_InventoryRecords_VariantId",
                table: "InventoryRecords",
                newName: "IX_InventoryRecords_MaterialId");

            migrationBuilder.AlterColumn<decimal>(
                name: "GrossQuantityRequired",
                table: "TaskMaterialRequirements",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)",
                oldDefaultValue: 0m);

            migrationBuilder.AlterColumn<decimal>(
                name: "Quantity",
                table: "OrderLineItems",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,4)");

            migrationBuilder.CreateIndex(
                name: "IX_TaskMaterialRequirements_TaskId",
                table: "TaskMaterialRequirements",
                column: "TaskId");

            migrationBuilder.CreateIndex(
                name: "IX_SupplierCatalogs_SupplierId",
                table: "SupplierCatalogs",
                column: "SupplierId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryRecords_WarehouseId",
                table: "InventoryRecords",
                column: "WarehouseId");

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryRecords_Materials_MaterialId",
                table: "InventoryRecords",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequisitions_Materials_MaterialId",
                table: "MaterialsRequisitions",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_OrderLineItems_Materials_MaterialId",
                table: "OrderLineItems",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressReports_UserAccounts_EngineerId",
                table: "ProgressReports",
                column: "EngineerId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SupplierCatalogs_Materials_MaterialId",
                table: "SupplierCatalogs",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskMaterialRequirements_Materials_MaterialId",
                table: "TaskMaterialRequirements",
                column: "MaterialId",
                principalTable: "Materials",
                principalColumn: "MaterialId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
