using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations;

[DbContext(typeof(AppDbContext))]
[Migration("20260717114529_HardenWorkflowConcurrencyAndCosts")]
public sealed class HardenWorkflowConcurrencyAndCosts : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "TaskItems", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "PurchaseOrders", type: "rowversion", rowVersion: true, nullable: false);
        migrationBuilder.AddColumn<decimal>(name: "ActualCostIncrement", table: "ProgressReports", type: "decimal(18,2)", nullable: false, defaultValue: 0m);
        migrationBuilder.AddColumn<byte[]>(name: "RowVersion", table: "MaterialsRequests", type: "rowversion", rowVersion: true, nullable: false);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(name: "RowVersion", table: "TaskItems");
        migrationBuilder.DropColumn(name: "RowVersion", table: "PurchaseOrders");
        migrationBuilder.DropColumn(name: "ActualCostIncrement", table: "ProgressReports");
        migrationBuilder.DropColumn(name: "RowVersion", table: "MaterialsRequests");
    }
}
