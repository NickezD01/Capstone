using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AlignSoftDeleteQueryFilters : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_UserAccounts_UserID",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemReports_Projects_ProjectID",
                table: "SystemReports");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemReports_UserAccounts_GeneratorId",
                table: "SystemReports");

            migrationBuilder.DropIndex(
                name: "IX_SystemReports_GeneratorId",
                table: "SystemReports");

            migrationBuilder.Sql(
                "UPDATE SystemReports SET GeneratedBy = GeneratorId WHERE GeneratorId <> 0;");

            migrationBuilder.DropColumn(
                name: "GeneratorId",
                table: "SystemReports");

            migrationBuilder.CreateIndex(
                name: "IX_SystemReports_GeneratedBy",
                table: "SystemReports",
                column: "GeneratedBy");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_UserAccounts_UserID",
                table: "Activities",
                column: "UserID",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemReports_Projects_ProjectID",
                table: "SystemReports",
                column: "ProjectID",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemReports_UserAccounts_GeneratedBy",
                table: "SystemReports",
                column: "GeneratedBy",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Activities_UserAccounts_UserID",
                table: "Activities");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemReports_Projects_ProjectID",
                table: "SystemReports");

            migrationBuilder.DropForeignKey(
                name: "FK_SystemReports_UserAccounts_GeneratedBy",
                table: "SystemReports");

            migrationBuilder.DropIndex(
                name: "IX_SystemReports_GeneratedBy",
                table: "SystemReports");

            migrationBuilder.AddColumn<int>(
                name: "GeneratorId",
                table: "SystemReports",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.Sql(
                "UPDATE SystemReports SET GeneratorId = GeneratedBy;");

            migrationBuilder.CreateIndex(
                name: "IX_SystemReports_GeneratorId",
                table: "SystemReports",
                column: "GeneratorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Activities_UserAccounts_UserID",
                table: "Activities",
                column: "UserID",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemReports_Projects_ProjectID",
                table: "SystemReports",
                column: "ProjectID",
                principalTable: "Projects",
                principalColumn: "ProjectId",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_SystemReports_UserAccounts_GeneratorId",
                table: "SystemReports",
                column: "GeneratorId",
                principalTable: "UserAccounts",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
