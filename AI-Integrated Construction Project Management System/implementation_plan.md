# Implementation Plan - Restructuring: Task-to-Phase Migration Slice

## Context & Architecture Overview

The system is transitioning from a flat project-task model to a hierarchical structure:
```text
Project -> Phase -> Task -> Material requirement -> Material request -> Inventory issue
```

### Current Status in Codebase:
1. **Phase Entity & API (Slice 1)**:
   - Completed in code (`Phase.cs`, `PhaseConfiguration.cs`, `PhasesController.cs`, `PhaseService.cs`).
   - Migration `20260908215306_AddPhases` has been generated (adds `Phases` table, adds nullable `TaskItems.PhaseId`), but is **not yet applied** to the production database.
   - All 119 unit/regression tests currently pass.
2. **The Immediate Next Move (Slice 2 - Task-to-Phase Migration)**:
   - This slice corresponds to Steps 6–7 and 9 in `BACKEND_RESTRUCTURING_PLAN.md`.
   - The user provided four screenshots detailing the requirements for migrating `TaskItem` to have a required `PhaseId`, generating the backfill migration, restructuring task routes/DTOs, deprecating old create routes with `410 Gone`, and maintaining read backward-compatibility aliases.

---

## User Review Required

> [!IMPORTANT]
> **Database Migration Sequencing:**
> - `20260908215306_AddPhases` must be applied first to the target database (after backup) before the new backfill migration can run.
> - The new migration `BackfillGeneralPhasesAndRequireTaskPhaseId` will execute raw SQL inside its `Up` method to:
>   1. Insert a `'General'` phase for every non-deleted project missing one (`SequenceOrder = 0`, `BaselineStart`/`BaselineEnd` matching project baseline, `Status = 'PLANNED'`).
>   2. Backfill `TaskItems.PhaseId` to point to the project's `General` phase wherever `PhaseId IS NULL`.
>   3. Check for any remaining orphaned/null `PhaseId` rows and throw an error to halt the migration if any exist.
>   4. Alter `TaskItems.PhaseId` to be non-nullable (`NOT NULL`).
> - We will generate this migration and inspect its SQL, but will **not apply it to live databases without user approval**.

> [!WARNING]
> **Breaking Change on Task Creation Writes:**
> - `POST /api/task` will now return HTTP `410 Gone` with a helpful deprecation message instead of accepting tasks with free-text `phaseName`.
> - Clients must switch to `POST /api/Phases/{phaseId}/tasks`.
> - Existing read endpoints (`GET /api/task/project/{id}` and `GET /api/task/{id}`) will remain as working aliases alongside canonical REST routes `GET /api/Projects/{projectId}/tasks` and `GET /api/Tasks/{taskId}`.

---

## Proposed Improvements to Old Plan

Based on analyzing the codebase and the notes in the screenshots:
1. **Date Validation Stacking**: Task dates must be strictly validated against *both* the Project baseline *and* the Phase baseline:
   `project.BaselineStart <= task.BaselineStart && task.BaselineEnd <= project.BaselineEnd` AND
   `phase.BaselineStart <= task.BaselineStart && task.BaselineEnd <= phase.BaselineEnd`.
2. **Phase Status Guard**: A task cannot be created under or moved to a phase that is `COMPLETED` or `CANCELLED`.
3. **Cross-Project Phase Guard**: On `PUT /api/Tasks/{taskId}`, if `PhaseId` is changed, the new phase must belong to the exact same `ProjectId` as the task.
4. **Denormalized `PhaseName` Sync**: While `PhaseId` becomes the single source of truth, `TaskItems.PhaseName` is retained in the database table and populated automatically from `Phase.Name` during create and update. This prevents breaking consumers that still display the phase name without querying the phase entity.
5. **Route Architecture**: Standardize controller naming:
   - Create canonical `TasksController` (or augment `TaskController`) with route attributes handling `api/Tasks` and `api/task` (case-insensitive alias).
   - Expose `POST /api/Phases/{phaseId:int}/tasks` and `GET /api/Projects/{projectId:int}/tasks`.
   - Implement `POST /api/task` with explicit `410 Gone`.

---

## Proposed Changes

### 1. Domain & Persistence Layer

#### [MODIFY] [TaskItem.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Domain/Models/TaskItem.cs)
- Change `public int? PhaseId { get; set; }` to `public int PhaseId { get; set; }` (required).
- Keep `public string PhaseName { get; set; } = null!;` for denormalized compatibility.
- Update `UpdatePlan` method:
  ```csharp
  public void UpdatePlan(int phaseId, string phaseName, string taskName, int assigneeId, decimal plannedBudget,
      DateTime baselineStart, DateTime baselineEnd)
  ```
  Validate `phaseId > 0`, non-empty `phaseName`, and assign `PhaseId = phaseId; PhaseName = phaseName.Trim();`.

#### [MODIFY] [TaskItemConfiguration.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Infrastructure/Configuration/TaskItemConfiguration.cs)
- Configure `PhaseId` as required:
  ```csharp
  builder.Property(t => t.PhaseId).IsRequired();
  builder.HasOne(t => t.Phase)
         .WithMany(p => p.Tasks)
         .HasForeignKey(t => t.PhaseId)
         .IsRequired()
         .OnDelete(DeleteBehavior.Restrict);
  ```

#### [NEW] Migration: `BackfillGeneralPhasesAndRequireTaskPhaseId`
- Generated using `dotnet ef migrations add BackfillGeneralPhasesAndRequireTaskPhaseId`.
- In `Up(MigrationBuilder migrationBuilder)`:
  - Raw SQL to insert `'General'` phase for projects lacking one.
  - Raw SQL to backfill null `PhaseId` in `TaskItems`.
  - Raw SQL validation check ensuring 0 nulls remain.
  - Alter column `TaskItems.PhaseId` to `int` NOT NULL.
- In `Down(MigrationBuilder migrationBuilder)`:
  - Alter column `TaskItems.PhaseId` to `int` NULL.

---

### 2. Application Layer (DTOs, Mapping & Services)

#### [MODIFY] [CreateTaskRequest.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/Request/Tasks/CreateTaskRequest.cs)
- Remove `ProjectId` and `PhaseName` properties (as `phaseId` is in the URL route and project is derived from phase).
- Retain `TaskName`, `AssignedToUserID`, `PlannedBudget`, `BaselineStart`, `BaselineEnd`, `Materials`.

#### [MODIFY] [TaskMutationRequests.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/Request/Tasks/TaskMutationRequests.cs)
- In `UpdateTaskRequest`:
  - Drop `public string PhaseName { get; set; }`.
  - Add `public int PhaseId { get; set; }`.

#### [MODIFY] [TaskResponse.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/Response/Tasks/TaskResponse.cs)
- Add `public int PhaseId { get; set; }`.
- Keep `public string PhaseName { get; set; } = null!;`.

#### [MODIFY] [MapperConfigurationsProfile.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/MyMapper/MapperConfigurationsProfile.cs)
- Update `CreateMap<CreateTaskRequest, TaskItem>()` to ignore `ProjectId`, `PhaseId`, `PhaseName` (set explicitly in service).
- Ensure `TaskItem -> TaskResponse` maps `PhaseId` directly.

#### [MODIFY] [ITaskService.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/Interfaces/ITaskService.cs)
- Update signatures:
  - `Task<ApiResponse> CreateTaskAsync(int phaseId, CreateTaskRequest request);`
  - Maintain `UpdateTaskAsync(int taskId, UpdateTaskRequest request);`
  - Add or maintain `GetTasksByProjectAsync(int projectId);`

#### [MODIFY] [TaskService.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Application/Services/TaskService.cs)
- In `CreateTaskAsync(int phaseId, CreateTaskRequest request)`:
  1. Retrieve `Phase` with `Project` (return 404 if not found).
  2. Verify PM ownership of the project (403 if caller is not owning PM).
  3. Validate project status (409 if `COMPLETED` or `CANCELLED`).
  4. Validate phase status (409 if `COMPLETED` or `CANCELLED`).
  5. Validate dates: `request.BaselineStart` & `request.BaselineEnd` must be within `project.BaselineStart..project.BaselineEnd` AND `phase.BaselineStart..phase.BaselineEnd` (400 if violation).
  6. Validate total planned budget against project budget limit (409 if exceeded).
  7. Map task, assign `task.PhaseId = phase.PhaseId`, `task.ProjectId = phase.ProjectId`, `task.PhaseName = phase.Name`, `task.AssignedToUserID = currentUser.Id`.
  8. Save and process material requirements.
- In `UpdateTaskAsync(int taskId, UpdateTaskRequest request)`:
  1. Retrieve `TaskItem`. Return 404 if not found.
  2. Retrieve target `Phase` by `request.PhaseId`. Return 404 if not found.
  3. If `phase.ProjectId != task.ProjectId`, return 400 Bad Request ("Phase does not belong to the project").
  4. If `phase.Status is PhaseStatus.COMPLETED or PhaseStatus.CANCELLED`, return 409 Conflict.
  5. Verify owning PM (403).
  6. Verify row version concurrency (409).
  7. Validate dates within project baseline AND phase baseline.
  8. Call `task.UpdatePlan(phase.PhaseId, phase.Name, request.TaskName, ...)` and persist.

---

### 3. API Layer (Controllers & Routes)

#### [MODIFY] [TaskController.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_API/Controllers/TaskController.cs)
- Add route mapping for both `api/Tasks` and `api/task`.
- Add `POST /api/Phases/{phaseId:int}/tasks` -> calls `_taskService.CreateTaskAsync(phaseId, request)`.
- Add `GET /api/Projects/{projectId:int}/tasks` -> calls `_taskService.GetTasksByProjectAsync(projectId)`.
- Support canonical `GET /api/Tasks/{taskId:int}` and alias `GET /api/task/{taskId:int}`.
- Support canonical `PUT /api/Tasks/{taskId:int}` and alias `PUT /api/task/{taskId:int}`.
- Retain lifecycle endpoints: `POST /api/Tasks/{taskId}/cancel`, `reject`, `reopen` (with `/api/task/...` aliases).
- Legacy create endpoint:
  ```csharp
  [HttpPost]
  [Authorize(Roles = "PM")]
  public IActionResult DeprecatedCreateTask()
  {
      return StatusCode(StatusCodes.Status410Gone, new ApiResponse().SetApiResponse(
          HttpStatusCode.Gone, false, "POST /api/task is deprecated. Use POST /api/Phases/{phaseId}/tasks instead."));
  }
  ```

---

### 4. Tests & Documentation

#### [MODIFY] [EfModelTests.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Tests/EfModelTests.cs)
- Update `Phase_HasProjectRelationshipUniquenessConcurrencyAndNullableTaskLink` -> rename and assert that `phaseFk.IsRequired == true` on `TaskItem`, while `PhaseName` is still mapped and not nullable.

#### [MODIFY] [BusinessRuleRegressionTests.cs](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/cpms_Tests/BusinessRuleRegressionTests.cs)
- Update existing `CreateTaskAsync` tests to call the new signature with `phaseId` and verify new assertions.
- Add test: `CreateTask_RejectsDatesOutsidePhaseBaseline`.
- Add test: `CreateTask_RejectsClosedPhase`.
- Add test: `UpdateTask_RejectsPhaseFromAnotherProject`.
- Add test: `DeprecatedCreateTask_Returns410Gone`.

#### [NEW] [TASK_PHASE_MIGRATION_PLAN.md](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/TASK_PHASE_MIGRATION_PLAN.md)
- Documents the schema migration, data backfill logic, API route changes, and rollout instructions.

#### [MODIFY] [FRONTEND_API_REFERENCE.md](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/FRONTEND_API_REFERENCE.md)
- Update task routes (`POST /api/Phases/{phaseId}/tasks`, `GET /api/Projects/{projectId}/tasks`, etc.).
- Document `410 Gone` on `POST /api/task`.

#### [MODIFY] [ROLE_API_ACCESS_MATRIX.md](file:///d:/project/GitHub/Capstone/AI-Integrated%20Construction%20Project%20Management%20System/ROLE_API_ACCESS_MATRIX.md)
- Update task section with new routes and permissions.

---

## Verification Plan

### Automated Tests
1. Run complete test suite:
   ```powershell
   dotnet test "AI-Integrated Construction Project Management System"
   ```
2. Verify EF model tests pass with required `PhaseId`.
3. Verify all business rule tests pass with phase-scoped creation, cross-project phase rejection, and date boundary checks.

### Migration Inspection
1. Generate migration script to review raw SQL without applying to DB:
   ```powershell
   dotnet ef migrations script 20260908215306_AddPhases --project cpms_Infrastructure --startup-project cpms_API
   ```
2. Verify that backfill statements correctly create `'General'` phase and update `TaskItems.PhaseId`.
