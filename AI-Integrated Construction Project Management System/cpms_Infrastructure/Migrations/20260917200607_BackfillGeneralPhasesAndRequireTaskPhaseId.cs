using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace cpms_Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class BackfillGeneralPhasesAndRequireTaskPhaseId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. For every non-deleted project, insert one General phase if missing:
            //    SequenceOrder = 0, dates = project baseline, Status = PLANNED.
            migrationBuilder.Sql(@"
INSERT INTO [Phases] ([ProjectId], [Name], [Description], [SequenceOrder], [BaselineStart], [BaselineEnd], [Status], [IsDeleted], [CreatedDate])
SELECT p.[ProjectId],
       N'General',
       N'Default general phase for legacy tasks',
       0,
       p.[BaselineStart],
       p.[BaselineEnd],
       N'PLANNED',
       0,
       GETUTCDATE()
FROM [Projects] p
WHERE p.[IsDeleted] = 0
  AND NOT EXISTS (
      SELECT 1 FROM [Phases] ph
      WHERE ph.[ProjectId] = p.[ProjectId]
        AND ph.[Name] = N'General'
        AND ph.[IsDeleted] = 0
  );
");

            // 2. Set TaskItems.PhaseId to that project's General phase wherever it is null.
            migrationBuilder.Sql(@"
UPDATE t
SET t.[PhaseId] = ph.[PhaseId]
FROM [TaskItems] t
INNER JOIN [Phases] ph ON ph.[ProjectId] = t.[ProjectId]
                      AND ph.[Name] = N'General'
                      AND ph.[IsDeleted] = 0
WHERE t.[PhaseId] IS NULL;
");

            // 3. Validate zero remaining null PhaseId rows (including soft-deleted tasks).
            //    If any remain, fail the migration rather than silently dropping them.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM [TaskItems] WHERE [PhaseId] IS NULL)
BEGIN
    THROW 50000, 'Migration failed: TaskItems still contain NULL PhaseId values after General phase backfill.', 1;
END
");

            // 4. Alter TaskItems.PhaseId to required (NOT NULL).
            migrationBuilder.AlterColumn<int>(
                name: "PhaseId",
                table: "TaskItems",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<int>(
                name: "PhaseId",
                table: "TaskItems",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
