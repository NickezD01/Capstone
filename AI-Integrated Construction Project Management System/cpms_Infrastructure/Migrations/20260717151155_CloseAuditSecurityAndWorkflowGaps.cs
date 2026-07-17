using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class CloseAuditSecurityAndWorkflowGaps : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects");

            migrationBuilder.AddColumn<string>(
                name: "PendingMfaSecretProtected",
                table: "UserAccounts",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "SupplierMetrics",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddColumn<string>(
                name: "ParentTokenHash",
                table: "RefreshTokens",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReuseDetectedAt",
                table: "RefreshTokens",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "SessionFamilyId",
                table: "RefreshTokens",
                type: "uniqueidentifier",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.Sql("UPDATE [RefreshTokens] SET [SessionFamilyId] = NEWID() WHERE [SessionFamilyId] = '00000000-0000-0000-0000-000000000000';");

            migrationBuilder.AddColumn<byte[]>(
                name: "ExpectedInventoryRowVersion",
                table: "PhysicalCountLines",
                type: "binary(8)",
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateTable(
                name: "EmailOutboxMessages",
                columns: table => new
                {
                    MessageId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Recipient = table.Column<string>(type: "nvarchar(320)", maxLength: 320, nullable: false),
                    ProtectedHtmlBody = table.Column<string>(type: "nvarchar(max)", maxLength: 8000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()"),
                    NextAttemptAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AttemptCount = table.Column<int>(type: "int", nullable: false),
                    ProcessedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    LastError = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmailOutboxMessages", x => x.MessageId);
                    table.CheckConstraint("CK_EmailOutboxMessages_AttemptCount", "[AttemptCount] >= 0 AND [AttemptCount] <= 10");
                });

            migrationBuilder.Sql("UPDATE [Projects] SET [StartDate] = [BaselineStart] WHERE [StartDate] < [BaselineStart];");

            migrationBuilder.Sql("""
                ;WITH DuplicateOpenCounts AS
                (
                    SELECT [SessionId], ROW_NUMBER() OVER
                        (PARTITION BY [WarehouseId] ORDER BY [StartedAt] DESC, [SessionId] DESC) AS [RowNumber]
                    FROM [PhysicalCountSessions]
                    WHERE [Status] IN ('DRAFT','PENDING_APPROVAL')
                )
                UPDATE session SET [Status] = 'REJECTED'
                FROM [PhysicalCountSessions] session
                INNER JOIN DuplicateOpenCounts duplicate ON duplicate.[SessionId] = session.[SessionId]
                WHERE duplicate.[RowNumber] > 1;
                """);

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId_SessionFamilyId",
                table: "RefreshTokens",
                columns: new[] { "UserId", "SessionFamilyId" });

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects",
                sql: "[StartDate] >= [BaselineStart] AND [StartDate] <= [BaselineEnd]");

            migrationBuilder.CreateIndex(
                name: "IX_PhysicalCountSessions_WarehouseId",
                table: "PhysicalCountSessions",
                column: "WarehouseId",
                unique: true,
                filter: "[Status] IN ('DRAFT','PENDING_APPROVAL')");

            migrationBuilder.CreateIndex(
                name: "IX_EmailOutboxMessages_ProcessedAt_NextAttemptAt",
                table: "EmailOutboxMessages",
                columns: new[] { "ProcessedAt", "NextAttemptAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmailOutboxMessages");

            migrationBuilder.DropIndex(
                name: "IX_RefreshTokens_UserId_SessionFamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects");

            migrationBuilder.DropIndex(
                name: "IX_PhysicalCountSessions_WarehouseId",
                table: "PhysicalCountSessions");

            migrationBuilder.DropColumn(
                name: "PendingMfaSecretProtected",
                table: "UserAccounts");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "SupplierMetrics");

            migrationBuilder.DropColumn(
                name: "ParentTokenHash",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ReuseDetectedAt",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "SessionFamilyId",
                table: "RefreshTokens");

            migrationBuilder.DropColumn(
                name: "ExpectedInventoryRowVersion",
                table: "PhysicalCountLines");

            migrationBuilder.CreateIndex(
                name: "IX_RefreshTokens_UserId",
                table: "RefreshTokens",
                column: "UserId");

            migrationBuilder.AddCheckConstraint(
                name: "CK_Projects_StartDate",
                table: "Projects",
                sql: "[StartDate] <= [BaselineEnd]");
        }
    }
}
