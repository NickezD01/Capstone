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
                name: "CustomerUserID",
                table: "Projects",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Projects_CustomerUserID",
                table: "Projects",
                column: "CustomerUserID");

            migrationBuilder.AddForeignKey(
                name: "FK_Projects_UserAccounts_CustomerUserID",
                table: "Projects",
                column: "CustomerUserID",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Projects_UserAccounts_CustomerUserID",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_Projects_CustomerUserID",
                table: "Projects");

            migrationBuilder.DropColumn(
                name: "CustomerUserID",
                table: "Projects");
        }
    }
}
