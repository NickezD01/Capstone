using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveWorkCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Phases_WorkCategories_WorkCategoryId",
                table: "Phases");

            migrationBuilder.DropTable(
                name: "WorkCategories");

            migrationBuilder.DropIndex(
                name: "IX_Phases_WorkCategoryId",
                table: "Phases");

            migrationBuilder.DropColumn(
                name: "WorkCategoryId",
                table: "Phases");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "WorkCategoryId",
                table: "Phases",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "WorkCategories",
                columns: table => new
                {
                    WorkCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCategories", x => x.WorkCategoryId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phases_WorkCategoryId",
                table: "Phases",
                column: "WorkCategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_WorkCategories_Name",
                table: "WorkCategories",
                column: "Name",
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.AddForeignKey(
                name: "FK_Phases_WorkCategories_WorkCategoryId",
                table: "Phases",
                column: "WorkCategoryId",
                principalTable: "WorkCategories",
                principalColumn: "WorkCategoryId",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
