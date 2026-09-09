using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    public partial class AddPhases : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PhaseId",
                table: "TaskItems",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Phases",
                columns: table => new
                {
                    PhaseId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjectId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    SequenceOrder = table.Column<int>(type: "int", nullable: false),
                    BaselineStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    BaselineEnd = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false),
                    CreatedDate = table.Column<DateTime>(type: "datetime2", nullable: true, defaultValueSql: "GETUTCDATE()"),
                    ModifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CreatedBy = table.Column<int>(type: "int", nullable: true),
                    ModifiedBy = table.Column<int>(type: "int", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false, defaultValue: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Phases", x => x.PhaseId);
                    table.CheckConstraint("CK_Phases_BaselineDates", "[BaselineEnd] >= [BaselineStart]");
                    table.CheckConstraint("CK_Phases_SequenceOrder", "[SequenceOrder] >= 0");
                    table.ForeignKey(
                        name: "FK_Phases_Projects_ProjectId",
                        column: x => x.ProjectId,
                        principalTable: "Projects",
                        principalColumn: "ProjectId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Phases_ProjectId_Name",
                table: "Phases",
                columns: new[] { "ProjectId", "Name" },
                unique: true,
                filter: "[IsDeleted] = 0");

            migrationBuilder.CreateIndex(
                name: "IX_Phases_ProjectId_SequenceOrder",
                table: "Phases",
                columns: new[] { "ProjectId", "SequenceOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_TaskItems_PhaseId",
                table: "TaskItems",
                column: "PhaseId");

            migrationBuilder.AddForeignKey(
                name: "FK_TaskItems_Phases_PhaseId",
                table: "TaskItems",
                column: "PhaseId",
                principalTable: "Phases",
                principalColumn: "PhaseId",
                onDelete: ReferentialAction.Restrict);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TaskItems_Phases_PhaseId",
                table: "TaskItems");

            migrationBuilder.DropTable(
                name: "Phases");

            migrationBuilder.DropIndex(
                name: "IX_TaskItems_PhaseId",
                table: "TaskItems");

            migrationBuilder.DropColumn(
                name: "PhaseId",
                table: "TaskItems");
        }
    }
}
