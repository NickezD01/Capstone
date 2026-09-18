using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddProjectCustomerAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CustomerUserId",
                table: "Projects",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerUserId",
                table: "Projects",
                column: "CustomerUserId");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserAccounts_CustomerUserId",
                table: "Projects",
                column: "CustomerUserId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserAccounts_CustomerUserId",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CustomerUserId",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CustomerUserId",
                table: "Projects");
        }
    }
}
