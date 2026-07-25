using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTaskIdRelationToMaterialRequestTable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_MaterialsRequests_TaskId",
                table: "MaterialsRequests",
                column: "TaskId");

            migrationBuilder.AddForeignKey(
                name: "FK_MaterialsRequests_TaskItems_TaskId",
                table: "MaterialsRequests",
                column: "TaskId",
                principalTable: "TaskItems",
                principalColumn: "TaskId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_MaterialsRequests_TaskItems_TaskId",
                table: "MaterialsRequests");

            migrationBuilder.DropIndex(
                name: "IX_MaterialsRequests_TaskId",
                table: "MaterialsRequests");
        }
    }
}
