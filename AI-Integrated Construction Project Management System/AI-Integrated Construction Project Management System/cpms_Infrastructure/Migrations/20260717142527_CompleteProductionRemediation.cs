using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CompleteProductionRemediation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                IF EXISTS (
                    SELECT UPPER(LTRIM(RTRIM([Email])))
                    FROM [UserAccounts]
                    GROUP BY UPPER(LTRIM(RTRIM([Email])))
                    HAVING COUNT(*) > 1)
                    THROW 51000, 'Duplicate normalized emails must be resolved before applying CompleteProductionRemediation.', 1;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_AIAlerts_UserAccounts_UserAccountId",
                table: "AIAlerts");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBudgetHistories_Projects_ProjectId",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_UserAccounts_UserAccountId",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WarehouseTransfers_Status",
                table: "WarehouseTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WarehouseTransferItems_ReceivedQuantity",
                table: "WarehouseTransferItems");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_Email",
                table: "UserAccounts");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_UserAccountId",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_UserId",
                table: "EmailVerifications");

            migrationBuilder.DropIndex(
                name: "IX_AIAlerts_UserAccountId",
                table: "AIAlerts");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "UserAccountId",
                table: "AIAlerts");

            migrationBuilder.AddColumn<decimal>(
                name: "DamagedQuantity",
                table: "WarehouseTransferItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "LostQuantity",
                table: "WarehouseTransferItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "WarehouseTransferItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FailedLoginAttempts",
                table: "UserAccounts",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LockoutEnd",
                table: "UserAccounts",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MfaEnabled",
                table: "UserAccounts",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "MfaSecretProtected",
                table: "UserAccounts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "PasswordChangedAt",
                table: "UserAccounts",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "UserAccounts",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AlterColumn<double>(
                name: "ReliabilityScore",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<double>(
                name: "DefectRatePct",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AlterColumn<double>(
                name: "AvgDeliveryDelay",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                defaultValue: 0.0,
                oldClrType: typeof(double),
                oldType: "float");

            migrationBuilder.AddColumn<int>(
                name: "EvaluatedOrderCount",
                table: "SupplierMetrics",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastEvaluatedAt",
                table: "SupplierMetrics",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "OnTimeDeliveryRatePct",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<double>(
                name: "QualityScore",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                defaultValue: 100.0);

            migrationBuilder.Sql("""
                UPDATE [RefreshTokens]
                SET [Token] = LOWER(CONVERT(varchar(64), HASHBYTES('SHA2_256', [Token]), 2))
                WHERE LEN([Token]) <> 64 OR [Token] LIKE '%[^0-9A-Fa-f]%';
                """);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(512)",
                oldMaxLength: 512);

            migrationBuilder.AddColumn<string>(
                name: "DeviceInfo",
                table: "RefreshTokens",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RevokedAt",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Projects",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProjectBudgetHistories",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<int>(
                name: "OriginalReportId",
                table: "ProgressReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewNote",
                table: "ProgressReports",
                type: "nvarchar(2000)",
                maxLength: 2000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "ProgressReports",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReviewedByUserId",
                table: "ProgressReports",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProgressReports",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "ProgressReports",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "APPROVED");

            migrationBuilder.AddColumn<decimal>(
                name: "DamagedQuantity",
                table: "OrderLineItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MissingQuantity",
                table: "OrderLineItems",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "BatchNumber",
                table: "InventoryTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpiryDate",
                table: "InventoryTransactions",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LotNumber",
                table: "InventoryTransactions",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SerialNumber",
                table: "InventoryTransactions",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "TotalValue",
                table: "InventoryTransactions",
                type: "decimal(38,8)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "UnitCost",
                table: "InventoryTransactions",
                type: "decimal(18,4)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "AverageUnitCost",
                table: "InventoryRecords",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "QuarantineQuantity",
                table: "InventoryRecords",
                type: "decimal(18,4)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<int>(
                name: "FailedAttempts",
                table: "EmailVerifications",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Purpose",
                table: "EmailVerifications",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "EMAIL_VERIFICATION");

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "Activities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Activities",
                type: "datetime2",
                nullable: true,
                defaultValueSql: "GETUTCDATE()",
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ActivityName",
                table: "Activities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AddColumn<string>(
                name: "ChangesJson",
                table: "Activities",
                type: "nvarchar(max)",
                maxLength: 8000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CorrelationId",
                table: "Activities",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EntityId",
                table: "Activities",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NormalizedEmail",
                table: "UserAccounts",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: false,
                computedColumnSql: "UPPER(LTRIM(RTRIM([Email])))",
                stored: true);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableQuantity",
                table: "InventoryRecords",
                type: "decimal(19,4)",
                nullable: false,
                computedColumnSql: "[QuantityOnHand] - [ReservedQuantity] - [QuarantineQuantity]",
                stored: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldComputedColumnSql: "[QuantityOnHand] - [ReservedQuantity]",
                oldStored: true);

            migrationBuilder.AddColumn<decimal>(
                name: "InventoryValue",
                table: "InventoryRecords",
                type: "decimal(38,8)",
                nullable: false,
                computedColumnSql: "[QuantityOnHand] * [AverageUnitCost]",
                stored: true);

            migrationBuilder.CreateTable(
                name: "AuthRateLimitEntries",
                columns: table => new
                {
                    PartitionKey = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    RequestCount = table.Column<int>(type: "int", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AuthRateLimitEntries", x => x.PartitionKey);
                    table.CheckConstraint("CK_AuthRateLimitEntries_RequestCount", "[RequestCount] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "InventoryAdjustments",
                columns: table => new
                {
                    AdjustmentId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    QuantityDelta = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventoryAdjustments", x => x.AdjustmentId);
                    table.CheckConstraint("CK_InventoryAdjustments_Quantity", "[QuantityDelta] <> 0");
                    table.CheckConstraint("CK_InventoryAdjustments_Reason", "[ReasonCode] IN ('CYCLE_COUNT','DAMAGE','LOSS','DATA_CORRECTION','OPENING_BALANCE')");
                    table.CheckConstraint("CK_InventoryAdjustments_Status", "[Status] IN ('PENDING','APPROVED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_MaterialVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "MaterialVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_UserAccounts_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_UserAccounts_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_InventoryAdjustments_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MaterialReturns",
                columns: table => new
                {
                    ReturnId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialRequestId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ReasonCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Condition = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    ReturnedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialReturns", x => x.ReturnId);
                    table.CheckConstraint("CK_MaterialReturns_Condition", "[Condition] IN ('USABLE','QUARANTINED')");
                    table.CheckConstraint("CK_MaterialReturns_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_MaterialReturns_Reason", "[ReasonCode] IN ('UNUSED','EXCESS_ISSUE','DAMAGED')");
                    table.ForeignKey(
                        name: "FK_MaterialReturns_MaterialVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "MaterialVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReturns_MaterialsRequests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialsRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReturns_UserAccounts_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialReturns_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MrpPlanningRuns",
                columns: table => new
                {
                    RunId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Version = table.Column<int>(type: "int", nullable: false),
                    CalculatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    CalculatedByUserId = table.Column<int>(type: "int", nullable: false),
                    SnapshotJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TransferRecommendationsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MrpPlanningRuns", x => x.RunId);
                    table.ForeignKey(
                        name: "FK_MrpPlanningRuns_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MrpPlanningRuns_UserAccounts_CalculatedByUserId",
                        column: x => x.CalculatedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MrpPlanningRuns_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountSessions",
                columns: table => new
                {
                    SessionId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    WarehouseId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedByUserId = table.Column<int>(type: "int", nullable: false),
                    ReviewedByUserId = table.Column<int>(type: "int", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    SubmittedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ReviewNote = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalCountSessions", x => x.SessionId);
                    table.CheckConstraint("CK_PhysicalCountSessions_Status", "[Status] IN ('DRAFT','PENDING_APPROVAL','APPROVED','REJECTED')");
                    table.ForeignKey(
                        name: "FK_PhysicalCountSessions_UserAccounts_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountSessions_UserAccounts_ReviewedByUserId",
                        column: x => x.ReviewedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountSessions_Warehouses_WarehouseId",
                        column: x => x.WarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TransferInventoryReservations",
                columns: table => new
                {
                    TransferReservationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    TransferItemId = table.Column<int>(type: "int", nullable: false),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TransferInventoryReservations", x => x.TransferReservationId);
                    table.CheckConstraint("CK_TransferInventoryReservations_Quantity", "[Quantity] > 0");
                    table.CheckConstraint("CK_TransferInventoryReservations_Status", "[Status] IN ('ACTIVE','CONSUMED','RELEASED')");
                    table.ForeignKey(
                        name: "FK_TransferInventoryReservations_InventoryRecords_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "InventoryRecords",
                        principalColumn: "InventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferInventoryReservations_WarehouseTransferItems_TransferItemId",
                        column: x => x.TransferItemId,
                        principalTable: "WarehouseTransferItems",
                        principalColumn: "TransferItemId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TransferInventoryReservations_WarehouseTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WarehouseTransfers",
                        principalColumn: "TransferId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PhysicalCountLines",
                columns: table => new
                {
                    LineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<int>(type: "int", nullable: false),
                    InventoryId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    ExpectedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ActualQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PhysicalCountLines", x => x.LineId);
                    table.CheckConstraint("CK_PhysicalCountLines_Actual", "[ActualQuantity] IS NULL OR [ActualQuantity] >= 0");
                    table.CheckConstraint("CK_PhysicalCountLines_Expected", "[ExpectedQuantity] >= 0");
                    table.ForeignKey(
                        name: "FK_PhysicalCountLines_InventoryRecords_InventoryId",
                        column: x => x.InventoryId,
                        principalTable: "InventoryRecords",
                        principalColumn: "InventoryId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountLines_MaterialVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "MaterialVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PhysicalCountLines_PhysicalCountSessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "PhysicalCountSessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_WarehouseTransfers_Status",
                table: "WarehouseTransfers",
                sql: "[Status] IN ('REQUESTED','APPROVED','IN_TRANSIT','RECEIVED','CLOSED_WITH_VARIANCE','REJECTED','CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WarehouseTransferItems_ReceivedQuantity",
                table: "WarehouseTransferItems",
                sql: "[ReceivedQuantity] >= 0 AND [DamagedQuantity] >= 0 AND [LostQuantity] >= 0 AND [ReceivedQuantity] + [DamagedQuantity] + [LostQuantity] <= [ShippedQuantity]");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_NormalizedEmail",
                table: "UserAccounts",
                column: "NormalizedEmail",
                unique: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskItems_ActualCost",
                table: "TaskItems",
                sql: "[ActualCost] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskItems_ActualProgressPct",
                table: "TaskItems",
                sql: "[ActualProgressPct] >= 0 AND [ActualProgressPct] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskItems_BaselineDates",
                table: "TaskItems",
                sql: "[BaselineEnd] >= [BaselineStart]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_TaskItems_PlannedBudget",
                table: "TaskItems",
                sql: "[PlannedBudget] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierMetrics_OrderCount",
                table: "SupplierMetrics",
                sql: "[EvaluatedOrderCount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_SupplierMetrics_Rates",
                table: "SupplierMetrics",
                sql: "[DefectRatePct] >= 0 AND [DefectRatePct] <= 100 AND [OnTimeDeliveryRatePct] >= 0 AND [OnTimeDeliveryRatePct] <= 100 AND [QualityScore] >= 0 AND [QualityScore] <= 100 AND [ReliabilityScore] >= 0 AND [ReliabilityScore] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_BaselineDates",
                table: "Projects",
                sql: "[BaselineEnd] >= [BaselineStart]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects",
                sql: "[StartDate] <= [BaselineEnd]");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_TotalBudget",
                table: "Projects",
                sql: "[TotalProjectBudget] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetHistories_UpdatedByUserId",
                table: "ProjectBudgetHistories",
                column: "UpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectBudgetHistories_NewBudget",
                table: "ProjectBudgetHistories",
                sql: "[NewBudget] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProjectBudgetHistories_PreviousBudget",
                table: "ProjectBudgetHistories",
                sql: "[PreviousBudget] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReports_OriginalReportId",
                table: "ProgressReports",
                column: "OriginalReportId");

            migrationBuilder.CreateIndex(
                name: "IX_ProgressReports_ReviewedByUserId",
                table: "ProgressReports",
                column: "ReviewedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgressReports_ActualCostIncrement",
                table: "ProgressReports",
                sql: "[ActualCostIncrement] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgressReports_ProgressIncrement",
                table: "ProgressReports",
                sql: "[ProgressIncrement] >= -100 AND [ProgressIncrement] <= 100");

            migrationBuilder.AddCheckConstraint(
                name: "CK_ProgressReports_Status",
                table: "ProgressReports",
                sql: "[Status] IN ('PENDING','APPROVED','REJECTED','CORRECTED','REVERSED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_OrderLineItems_DeliveryAccounting",
                table: "OrderLineItems",
                sql: "[DamagedQuantity] >= 0 AND [MissingQuantity] >= 0 AND [ReceivedQuantity] + [DamagedQuantity] + [MissingQuantity] <= [Quantity]");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryTransactions_SerialNumber",
                table: "InventoryTransactions",
                column: "SerialNumber",
                unique: true,
                filter: "[SerialNumber] IS NOT NULL");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions",
                sql: "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT','TRANSFER_OUT','TRANSFER_IN','PHYSICAL_COUNT')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords",
                sql: "[ReservedQuantity] >= 0 AND [QuarantineQuantity] >= 0 AND [ReservedQuantity] + [QuarantineQuantity] <= [QuantityOnHand]");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_UserId_Purpose_IsUsed_ExpiresAt",
                table: "EmailVerifications",
                columns: new[] { "UserId", "Purpose", "IsUsed", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_AuthRateLimitEntries_WindowStart",
                table: "AuthRateLimitEntries",
                column: "WindowStart");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_RequestedByUserId",
                table: "InventoryAdjustments",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_ReviewedByUserId",
                table: "InventoryAdjustments",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_Status_WarehouseId_RequestedAt",
                table: "InventoryAdjustments",
                columns: new[] { "Status", "WarehouseId", "RequestedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_VariantId",
                table: "InventoryAdjustments",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryAdjustments_WarehouseId",
                table: "InventoryAdjustments",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_MaterialRequestId_VariantId_ReturnedAt",
                table: "MaterialReturns",
                columns: new[] { "MaterialRequestId", "VariantId", "ReturnedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_RecordedByUserId",
                table: "MaterialReturns",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_VariantId",
                table: "MaterialReturns",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialReturns_WarehouseId",
                table: "MaterialReturns",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_MrpPlanningRuns_CalculatedAt",
                table: "MrpPlanningRuns",
                column: "CalculatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_MrpPlanningRuns_CalculatedByUserId",
                table: "MrpPlanningRuns",
                column: "CalculatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_MrpPlanningRuns_ProjectId_WarehouseId_Version",
                table: "MrpPlanningRuns",
                columns: new[] { "ProjectId", "WarehouseId", "Version" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MrpPlanningRuns_WarehouseId",
                table: "MrpPlanningRuns",
                column: "WarehouseId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountLines_InventoryId",
                table: "PhysicalCountLines",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountLines_SessionId_InventoryId",
                table: "PhysicalCountLines",
                columns: new[] { "SessionId", "InventoryId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountLines_VariantId",
                table: "PhysicalCountLines",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountSessions_CreatedByUserId",
                table: "PhysicalCountSessions",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountSessions_ReviewedByUserId",
                table: "PhysicalCountSessions",
                column: "ReviewedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountSessions_WarehouseId_Status_StartedAt",
                table: "PhysicalCountSessions",
                columns: new[] { "WarehouseId", "Status", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TransferInventoryReservations_InventoryId",
                table: "TransferInventoryReservations",
                column: "InventoryId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInventoryReservations_TransferId",
                table: "TransferInventoryReservations",
                column: "TransferId");

            migrationBuilder.CreateIndex(
                name: "IX_TransferInventoryReservations_TransferItemId",
                table: "TransferInventoryReservations",
                column: "TransferItemId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressReports_ProgressReports_OriginalReportId",
                table: "ProgressReports",
                column: "OriginalReportId",
                principalTable: "ProgressReports",
                principalColumn: "ReportId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProgressReports_UserAccounts_ReviewedByUserId",
                table: "ProgressReports",
                column: "ReviewedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBudgetHistories_Projects_ProjectId",
                table: "ProjectBudgetHistories",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBudgetHistories_UserAccounts_UpdatedByUserId",
                table: "ProjectBudgetHistories",
                column: "UpdatedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProgressReports_ProgressReports_OriginalReportId",
                table: "ProgressReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ProgressReports_UserAccounts_ReviewedByUserId",
                table: "ProgressReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBudgetHistories_Projects_ProjectId",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjectBudgetHistories_UserAccounts_UpdatedByUserId",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropTable(
                name: "AuthRateLimitEntries");

            migrationBuilder.DropTable(
                name: "InventoryAdjustments");

            migrationBuilder.DropTable(
                name: "MaterialReturns");

            migrationBuilder.DropTable(
                name: "MrpPlanningRuns");

            migrationBuilder.DropTable(
                name: "PhysicalCountLines");

            migrationBuilder.DropTable(
                name: "TransferInventoryReservations");

            migrationBuilder.DropTable(
                name: "PhysicalCountSessions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WarehouseTransfers_Status",
                table: "WarehouseTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_WarehouseTransferItems_ReceivedQuantity",
                table: "WarehouseTransferItems");

            migrationBuilder.DropIndex(
                name: "IX_UserAccounts_NormalizedEmail",
                table: "UserAccounts");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskItems_ActualCost",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskItems_ActualProgressPct",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskItems_BaselineDates",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_TaskItems_PlannedBudget",
                table: "TaskItems");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierMetrics_OrderCount",
                table: "SupplierMetrics");

            migrationBuilder.DropCheckConstraint(
                name: "CK_SupplierMetrics_Rates",
                table: "SupplierMetrics");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_BaselineDates",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_TotalBudget",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_ProjectBudgetHistories_UpdatedByUserId",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectBudgetHistories_NewBudget",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProjectBudgetHistories_PreviousBudget",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropIndex(
                name: "IX_ProgressReports_OriginalReportId",
                table: "ProgressReports");

            migrationBuilder.DropIndex(
                name: "IX_ProgressReports_ReviewedByUserId",
                table: "ProgressReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgressReports_ActualCostIncrement",
                table: "ProgressReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgressReports_ProgressIncrement",
                table: "ProgressReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_ProgressReports_Status",
                table: "ProgressReports");

            migrationBuilder.DropCheckConstraint(
                name: "CK_OrderLineItems_DeliveryAccounting",
                table: "OrderLineItems");

            migrationBuilder.DropIndex(
                name: "IX_InventoryTransactions_SerialNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropIndex(
                name: "IX_EmailVerifications_UserId_Purpose_IsUsed_ExpiresAt",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "NormalizedEmail",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "InventoryValue",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "DamagedQuantity",
                table: "WarehouseTransferItems");

            migrationBuilder.DropColumn(
                name: "LostQuantity",
                table: "WarehouseTransferItems");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "WarehouseTransferItems");

            migrationBuilder.DropColumn(
                name: "FailedLoginAttempts",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "LockoutEnd",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "MfaEnabled",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "MfaSecretProtected",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "PasswordChangedAt",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "EvaluatedOrderCount",
                table: "SupplierMetrics");

            migrationBuilder.DropColumn(
                name: "LastEvaluatedAt",
                table: "SupplierMetrics");

            migrationBuilder.DropColumn(
                name: "OnTimeDeliveryRatePct",
                table: "SupplierMetrics");

            migrationBuilder.DropColumn(
                name: "QualityScore",
                table: "SupplierMetrics");

            migrationBuilder.DropColumn(
                name: "DeviceInfo",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReplacedByTokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RevokedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProjectBudgetHistories");

            migrationBuilder.DropColumn(
                name: "OriginalReportId",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "ReviewNote",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "ReviewedByUserId",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "ProgressReports");

            migrationBuilder.DropColumn(
                name: "DamagedQuantity",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "MissingQuantity",
                table: "OrderLineItems");

            migrationBuilder.DropColumn(
                name: "BatchNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "ExpiryDate",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "LotNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "SerialNumber",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "TotalValue",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "UnitCost",
                table: "InventoryTransactions");

            migrationBuilder.DropColumn(
                name: "AverageUnitCost",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "QuarantineQuantity",
                table: "InventoryRecords");

            migrationBuilder.DropColumn(
                name: "FailedAttempts",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "Purpose",
                table: "EmailVerifications");

            migrationBuilder.DropColumn(
                name: "ChangesJson",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "CorrelationId",
                table: "Activities");

            migrationBuilder.DropColumn(
                name: "EntityId",
                table: "Activities");

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "TaskItems",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<double>(
                name: "ReliabilityScore",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 0.0);

            migrationBuilder.AlterColumn<double>(
                name: "DefectRatePct",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 0.0);

            migrationBuilder.AlterColumn<double>(
                name: "AvgDeliveryDelay",
                table: "SupplierMetrics",
                type: "float",
                nullable: false,
                oldClrType: typeof(double),
                oldType: "float",
                oldDefaultValue: 0.0);

            migrationBuilder.AlterColumn<string>(
                name: "Token",
                table: "RefreshTokens",
                type: "nvarchar(512)",
                maxLength: 512,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(64)",
                oldMaxLength: 64);

            migrationBuilder.AddColumn<int>(
                name: "UserAccountId",
                table: "AIAlerts",
                type: "int",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "EntityType",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(200)",
                oldMaxLength: 200);

            migrationBuilder.AlterColumn<DateTime>(
                name: "CreatedDate",
                table: "Activities",
                type: "datetime2",
                nullable: true,
                oldClrType: typeof(DateTime),
                oldType: "datetime2",
                oldNullable: true,
                oldDefaultValueSql: "GETUTCDATE()");

            migrationBuilder.AlterColumn<string>(
                name: "ActivityName",
                table: "Activities",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(100)",
                oldMaxLength: 100);

            migrationBuilder.AlterColumn<decimal>(
                name: "AvailableQuantity",
                table: "InventoryRecords",
                type: "decimal(19,4)",
                nullable: false,
                computedColumnSql: "[QuantityOnHand] - [ReservedQuantity]",
                stored: true,
                oldClrType: typeof(decimal),
                oldType: "decimal(19,4)",
                oldComputedColumnSql: "[QuantityOnHand] - [ReservedQuantity] - [QuarantineQuantity]",
                oldStored: true);

            migrationBuilder.AddCheckConstraint(
                name: "CK_WarehouseTransfers_Status",
                table: "WarehouseTransfers",
                sql: "[Status] IN ('REQUESTED','APPROVED','IN_TRANSIT','RECEIVED','REJECTED','CANCELLED')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_WarehouseTransferItems_ReceivedQuantity",
                table: "WarehouseTransferItems",
                sql: "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [ShippedQuantity]");

            migrationBuilder.CreateIndex(
                name: "IX_UserAccounts_Email",
                table: "UserAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_UserAccountId",
                table: "TaskItems",
                column: "UserAccountId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions",
                sql: "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT','TRANSFER_OUT','TRANSFER_IN')");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryRecords_ReservedQuantity",
                table: "InventoryRecords",
                sql: "[ReservedQuantity] >= 0 AND [ReservedQuantity] <= [QuantityOnHand]");

            migrationBuilder.CreateIndex(
                name: "IX_EmailVerifications_UserId",
                table: "EmailVerifications",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_AIAlerts_UserAccountId",
                table: "AIAlerts",
                column: "UserAccountId");

            migrationBuilder.AddForeignKey(
                name: "FK_AIAlerts_UserAccounts_UserAccountId",
                table: "AIAlerts",
                column: "UserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjectBudgetHistories_Projects_ProjectId",
                table: "ProjectBudgetHistories",
                column: "ProjectId",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_UserAccounts_UserAccountId",
                table: "TaskItems",
                column: "UserAccountId",
                principalTable: "UserAccounts",
                principalColumn: "Id");
        }
    }
}
