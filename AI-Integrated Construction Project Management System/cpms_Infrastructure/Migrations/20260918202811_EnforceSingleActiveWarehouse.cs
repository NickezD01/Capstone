using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class EnforceSingleActiveWarehouse : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "Warehouses",
                type: "bit",
                nullable: false,
                defaultValue: true);

            // Keep exactly one canonical operational warehouse: prefer the
            // lowest-id warehouse holding inventory, otherwise the lowest id.
            migrationBuilder.Sql(@"
DECLARE @CanonicalId INT = (
    SELECT TOP (1) w.WarehouseId
    FROM Warehouses w
    ORDER BY
        CASE WHEN EXISTS (SELECT 1 FROM InventoryRecords i WHERE i.WarehouseId = w.WarehouseId) THEN 0 ELSE 1 END,
        w.WarehouseId
);
UPDATE Warehouses SET IsActive = 0 WHERE WarehouseId <> @CanonicalId;
UPDATE Warehouses SET IsActive = 1 WHERE WarehouseId = @CanonicalId;");

            migrationBuilder.CreateIndex(
                name: "IX_Warehouses_IsActive",
                table: "Warehouses",
                column: "IsActive",
                unique: true,
                filter: "[IsActive] = 1 AND [IsDeleted] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Warehouses_IsActive",
                table: "Warehouses");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "Warehouses");
        }
    }
}
