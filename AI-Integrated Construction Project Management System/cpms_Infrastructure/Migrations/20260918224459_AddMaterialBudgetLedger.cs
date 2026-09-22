using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddMaterialBudgetLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "UnitActualCost",
                table: "MaterialsRequisitions",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "ActualCost",
                table: "MaterialsRequests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<DateTime>(
                name: "ActualCostUpdatedAt",
                table: "MaterialsRequests",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ActualCostUpdatedByUserId",
                table: "MaterialsRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BudgetDebitedAmount",
                table: "MaterialsRequests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedCost",
                table: "MaterialsRequests",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateTable(
                name: "MaterialBudgetTransactions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    RequestId = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<int>(type: "int", nullable: true),
                    VariantId = table.Column<int>(type: "int", nullable: true),
                    Quantity = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TransactionType = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    OldActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    NewActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebitedBefore = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    DebitedAfter = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    PerformedByUserId = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MaterialBudgetTransactions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MaterialBudgetTransactions_MaterialsRequests_RequestId",
                        column: x => x.RequestId,
                        principalTable: "MaterialsRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MaterialBudgetTransactions_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequisitions_UnitActualCost",
                table: "MaterialsRequisitions",
                sql: "[UnitActualCost] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_ActualCost",
                table: "MaterialsRequests",
                sql: "[ActualCost] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_BudgetDebited",
                table: "MaterialsRequests",
                sql: "[BudgetDebitedAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_EstimatedCost",
                table: "MaterialsRequests",
                sql: "[EstimatedCost] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialBudgetTransactions_ProjectId",
                table: "MaterialBudgetTransactions",
                column: "ProjectId");

            migrationBuilder.CreateIndex(
                name: "IX_MaterialBudgetTransactions_RequestId",
                table: "MaterialBudgetTransactions",
                column: "RequestId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MaterialBudgetTransactions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequisitions_UnitActualCost",
                table: "MaterialsRequisitions");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_ActualCost",
                table: "MaterialsRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_BudgetDebited",
                table: "MaterialsRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_EstimatedCost",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "UnitActualCost",
                table: "MaterialsRequisitions");

            migrationBuilder.DropColumn(
                name: "ActualCost",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "ActualCostUpdatedAt",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "ActualCostUpdatedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "BudgetDebitedAmount",
                table: "MaterialsRequests");

            migrationBuilder.DropColumn(
                name: "EstimatedCost",
                table: "MaterialsRequests");
        }
    }
}
