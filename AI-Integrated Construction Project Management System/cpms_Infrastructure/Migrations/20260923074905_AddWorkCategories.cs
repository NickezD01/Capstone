using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "WorkCategories",
                columns: table => new
                {
                    WorkCategoryId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WorkCategories", x => x.WorkCategoryId);
                });

            migrationBuilder.Sql(@"
INSERT INTO WorkCategories (Name, Description) VALUES
    (N'Structural', N'Foundations, frames, load-bearing and structural works'),
    (N'Finishing', N'Architectural finishes, painting, flooring and fit-out'),
    (N'MEP', N'Mechanical, electrical and plumbing works'),
    (N'External Works', N'Site, landscape, roads and external utilities'),
    (N'Preliminaries', N'Site setup, temporary works, permits and mobilization');");

            migrationBuilder.AddColumn<int>(
                name: "WorkCategoryId",
                table: "Phases",
                type: "int",
                nullable: true);

            // Existing phases predate categories: assign them to the first
            // starter category so the column can be made required below.
            migrationBuilder.Sql(@"
DECLARE @DefaultCategoryId INT = (SELECT MIN(WorkCategoryId) FROM WorkCategories WHERE IsDeleted = 0);
UPDATE Phases SET WorkCategoryId = @DefaultCategoryId WHERE WorkCategoryId IS NULL;");

            migrationBuilder.AlterColumn<int>(
                name: "WorkCategoryId",
                table: "Phases",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
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
    }
}
