# Task-to-Phase Migration Plan (Slice 2)

## Purpose

Make `TaskItem.PhaseId` a required foreign key so every task lives inside a phase. This
documents the schema migration, the backfill strategy, the API route changes, and the
rollout sequence. It corresponds to Steps 6-7 and 9 in `BACKEND_RESTRUCTURING_PLAN.md`.

## Target Hierarchy

```text
Project -> Phase -> Task -> Material requirement -> Material request -> Inventory issue
```

## Migration Sequencing (Important)

The two migrations must be applied in order:

1. `20260908215306_AddPhases` — adds the `Phases` table and a nullable `TaskItems.PhaseId`
   (Slice 1, already generated, may need to be applied first).
2. `20260917200607_BackfillGeneralPhasesAndRequireTaskPhaseId` — backfills data and makes
   `TaskItems.PhaseId` non-nullable.

```powershell
dotnet ef database update 20260908215306_AddPhases --project cpms_Infrastructure --startup-project cpms_API
dotnet ef database update --project cpms_Infrastructure --startup-project cpms_API
```

Always back up the target database before running either migration against production.

## Migration 2: Backfill + Require PhaseId

Generated as `BackfillGeneralPhasesAndRequireTaskPhaseId`. The `Up` method:

1. **Insert one `General` phase for every non-deleted project that does not have one**
   (`SequenceOrder = 0`, baseline dates copied from the project baseline, `Status = PLANNED`,
   soft-delete flag `0`):

   ```sql
   INSERT INTO [Phases] ([ProjectId], [Name], [Description], [SequenceOrder], [BaselineStart], [BaselineEnd], [Status], [IsDeleted], [CreatedDate])
   SELECT p.[ProjectId], N'General', N'Default general phase for legacy tasks', 0,
          p.[BaselineStart], p.[BaselineEnd], N'PLANNED', 0, GETUTCDATE()
   FROM [Projects] p
   WHERE p.[IsDeleted] = 0
     AND NOT EXISTS (
         SELECT 1 FROM [Phases] ph
         WHERE ph.[ProjectId] = p.[ProjectId]
           AND ph.[Name] = N'General'
           AND ph.[IsDeleted] = 0
     );
   ```

2. **Backfill `TaskItems.PhaseId`** to each task's project's `General` phase where null
   (this covers soft-deleted tasks too):

   ```sql
   UPDATE t
   SET t.[PhaseId] = ph.[PhaseId]
   FROM [TaskItems] t
   INNER JOIN [Phases] ph ON ph.[ProjectId] = t.[ProjectId]
                         AND ph.[Name] = N'General'
                         AND ph.[IsDeleted] = 0
   WHERE t.[PhaseId] IS NULL;
   ```

3. **Validation guard** — halt the migration if any `PhaseId` remains null rather than
   silently producing a constraint violation:

   ```sql
   IF EXISTS (SELECT 1 FROM [TaskItems] WHERE [PhaseId] IS NULL)
   BEGIN
       THROW 50000, 'Migration failed: TaskItems still contain NULL PhaseId values after General phase backfill.', 1;
   END
   ```

4. **Alter `TaskItems.PhaseId` to `NOT NULL`** (EF recreates the supporting index).

The `Down` method only restores the column to nullable; it does not delete the `General`
phases or restore `PhaseName` free text.

## Domain / Persistence Changes

- `TaskItem.PhaseId` is now `int` (required); `TaskItem.Phase` navigation is non-nullable.
- `TaskItem.PhaseName` is retained as a denormalized display column, populated automatically
  from `Phase.Name` during create/update (single source of truth is `PhaseId`).
- `TaskItemConfiguration` sets `PhaseId` as required with `DeleteBehavior.Restrict`
  (the FK direction is Phase -> Tasks, a phase cannot be deleted while tasks reference it).

## API Route Changes

| Direction | Old | New |
| --- | --- | --- |
| Create task | `POST /api/task` | `POST /api/Phases/{phaseId:int}/tasks` |
| List tasks by project | `GET /api/task/project/{projectId}` | `GET /api/Projects/{projectId:int}/tasks` (alias retained) |
| Task detail | `GET /api/task/{taskId}` | `GET /api/Tasks/{taskId:int}` (alias retained) |
| Update task | `PUT /api/task/{taskId}` | `PUT /api/Tasks/{taskId:int}` (alias retained) |
| Task material requirements | `GET /api/task/project/{projectId}/material-requirements` | `GET /api/Projects/{projectId:int}/material-requirements` (alias retained) |
| Assigned tasks | `GET /api/task/assigned` | `GET /api/Tasks/assigned` |
| Lifecycle | `POST /api/task/{taskId}/cancel\|reject\|reopen` | `POST /api/Tasks/{taskId}/cancel\|reject\|reopen` |

`POST /api/task` now returns **HTTP 410 Gone** with a message directing clients to
`POST /api/Phases/{phaseId}/tasks`.

## Request / Response Changes

- `CreateTaskRequest`: removed `ProjectId` and `PhaseName`. The project and phase are derived
  from the `phaseId` route parameter.
- `UpdateTaskRequest`: `PhaseName` replaced by `PhaseId`.
- `TaskResponse`: added `PhaseId`; `PhaseName` retained.
- Services validate: owning PM, project not `COMPLETED`/`CANCELLED`, phase not
  `COMPLETED`/`CANCELLED`, task dates inside both the project and phase baselines, and on
  update that the target phase belongs to the same project.

## Proposed Improvements (Compared to Original Plan)

- Task dates are validated against **both** the project baseline and the phase baseline.
- Tasks cannot be created under or moved into a `COMPLETED`/`CANCELLED` phase.
- On `PUT /api/Tasks/{taskId}`, a changed phase must belong to the exact same project.
- `PhaseName` is synced from `Phase.Name` during create and update to avoid breaking existing
  consumers that display the phase name without fetching the phase entity.

## Rollout Instructions

1. Back up the production database.
2. Apply `20260908215306_AddPhases` if not already applied.
3. Generate and review the SQL script without applying:
   ```powershell
   dotnet ef migrations script 20260908215306_AddPhases --project cpms_Infrastructure --startup-project cpms_API
   ```
4. Apply `20260917200607_BackfillGeneralPhasesAndRequireTaskPhaseId`.
5. Deploy the API (new routes + `410 Gone` on legacy create).
6. Update clients to call `POST /api/Phases/{phaseId}/tasks` and the canonical read routes.
7. Run the test suite:
   ```powershell
   dotnet test "AI-Integrated Construction Project Management System"
   ```

## Rollback

The `Down` migration only makes `PhaseId` nullable again. Reverting from a fully migrated
production database requires restoring the pre-migration backup; `General` phases are not
removed and backfilled `PhaseId` values are not cleared automatically.