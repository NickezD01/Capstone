using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase1MultiWarehouseTransfers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions");

            migrationBuilder.CreateTable(
                name: "WarehouseTransfers",
                columns: table => new
                {
                    TransferId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SourceWarehouseId = table.Column<int>(type: "int", nullable: false),
                    DestinationWarehouseId = table.Column<int>(type: "int", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false, defaultValue: "REQUESTED"),
                    RequestedByUserId = table.Column<int>(type: "int", nullable: false),
                    ApprovedByUserId = table.Column<int>(type: "int", nullable: true),
                    ShippedByUserId = table.Column<int>(type: "int", nullable: true),
                    ReceivedByUserId = table.Column<int>(type: "int", nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    ApprovedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ShippedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransfers", x => x.TransferId);
                    table.CheckConstraint("CK_WarehouseTransfers_DifferentWarehouses", "[SourceWarehouseId] <> [DestinationWarehouseId]");
                    table.CheckConstraint("CK_WarehouseTransfers_Status", "[Status] IN ('REQUESTED','APPROVED','IN_TRANSIT','RECEIVED','REJECTED','CANCELLED')");
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_UserAccounts_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_UserAccounts_ReceivedByUserId",
                        column: x => x.ReceivedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_UserAccounts_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_UserAccounts_ShippedByUserId",
                        column: x => x.ShippedByUserId,
                        principalTable: "UserAccounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_DestinationWarehouseId",
                        column: x => x.DestinationWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransfers_Warehouses_SourceWarehouseId",
                        column: x => x.SourceWarehouseId,
                        principalTable: "Warehouses",
                        principalColumn: "WarehouseId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "WarehouseTransferItems",
                columns: table => new
                {
                    TransferItemId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferId = table.Column<int>(type: "int", nullable: false),
                    VariantId = table.Column<int>(type: "int", nullable: false),
                    RequestedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    ShippedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    ReceivedQuantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false, defaultValue: 0m),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WarehouseTransferItems", x => x.TransferItemId);
                    table.CheckConstraint("CK_WarehouseTransferItems_ReceivedQuantity", "[ReceivedQuantity] >= 0 AND [ReceivedQuantity] <= [ShippedQuantity]");
                    table.CheckConstraint("CK_WarehouseTransferItems_RequestedQuantity", "[RequestedQuantity] > 0");
                    table.CheckConstraint("CK_WarehouseTransferItems_ShippedQuantity", "[ShippedQuantity] >= 0 AND [ShippedQuantity] <= [RequestedQuantity]");
                    table.ForeignKey(
                        name: "FK_WarehouseTransferItems_MaterialVariants_VariantId",
                        column: x => x.VariantId,
                        principalTable: "MaterialVariants",
                        principalColumn: "VariantId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_WarehouseTransferItems_WarehouseTransfers_TransferId",
                        column: x => x.TransferId,
                        principalTable: "WarehouseTransfers",
                        principalColumn: "TransferId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions",
                sql: "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT','TRANSFER_OUT','TRANSFER_IN')");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferItems_TransferId_VariantId",
                table: "WarehouseTransferItems",
                columns: new[] { "TransferId", "VariantId" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransferItems_VariantId",
                table: "WarehouseTransferItems",
                column: "VariantId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ApprovedByUserId",
                table: "WarehouseTransfers",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_DestinationWarehouseId_Status",
                table: "WarehouseTransfers",
                columns: new[] { "DestinationWarehouseId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ReceivedByUserId",
                table: "WarehouseTransfers",
                column: "ReceivedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_RequestedAt",
                table: "WarehouseTransfers",
                column: "RequestedAt");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_RequestedByUserId",
                table: "WarehouseTransfers",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_ShippedByUserId",
                table: "WarehouseTransfers",
                column: "ShippedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_WarehouseTransfers_SourceWarehouseId_Status",
                table: "WarehouseTransfers",
                columns: new[] { "SourceWarehouseId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "WarehouseTransferItems");

            migrationBuilder.DropTable(
                name: "WarehouseTransfers");

            migrationBuilder.DropCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions");

            migrationBuilder.AddCheckConstraint(
                name: "CK_InventoryTransactions_Type",
                table: "InventoryTransactions",
                sql: "[TransactionType] IN ('RECEIPT','ISSUE','RETURN','ADJUSTMENT')");
        }
    }
}
