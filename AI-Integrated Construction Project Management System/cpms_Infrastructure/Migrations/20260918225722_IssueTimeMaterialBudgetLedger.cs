using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class IssueTimeMaterialBudgetLedger : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ProjectBudgetLedgers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    MaterialRequestId = table.Column<int>(type: "int", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    ActualCost = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    BudgetDebitedAmount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EntryType = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    RecordedByUserId = table.Column<int>(type: "int", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    Note = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectBudgetLedgers", x => x.Id);
                    table.CheckConstraint("CK_ProjectBudgetLedgers_ActualCost", "[ActualCost] >= 0");
                    table.CheckConstraint("CK_ProjectBudgetLedgers_EntryType", "[EntryType] IN ('ISSUE','CORRECTION','RETURN')");
                    table.ForeignKey(
                        name: "FK_ProjectBudgetLedgers_MaterialsRequests_MaterialRequestId",
                        column: x => x.MaterialRequestId,
                        principalTable: "MaterialsRequests",
                        principalColumn: "RequestId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProjectBudgetLedgers_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MaterialsRequests_ActualCostUpdatedByUserId",
                table: "MaterialsRequests",
                column: "ActualCostUpdatedByUserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_ActualCost",
                table: "MaterialsRequests",
                sql: "[ActualCost] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_BudgetDebitedAmount",
                table: "MaterialsRequests",
                sql: "[BudgetDebitedAmount] >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "CK_MaterialsRequests_EstimatedCost",
                table: "MaterialsRequests",
                sql: "[EstimatedCost] >= 0");

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetLedgers_MaterialRequestId_RecordedAt",
                table: "ProjectBudgetLedgers",
                columns: new[] { "MaterialRequestId", "RecordedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjectBudgetLedgers_ProjectId_RecordedAt",
                table: "ProjectBudgetLedgers",
                columns: new[] { "ProjectId", "RecordedAt" });

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequests_UserAccounts_ActualCostUpdatedByUserId",
                table: "MaterialsRequests",
                column: "ActualCostUpdatedByUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequests_UserAccounts_ActualCostUpdatedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropTable(
                name: "ProjectBudgetLedgers");

            migrationBuilder.DropIndex(
                name: "IX_MaterialsRequests_ActualCostUpdatedByUserId",
                table: "MaterialsRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_ActualCost",
                table: "MaterialsRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_BudgetDebitedAmount",
                table: "MaterialsRequests");

            migrationBuilder.DropCheckConstraint(
                name: "CK_MaterialsRequests_EstimatedCost",
                table: "MaterialsRequests");

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
