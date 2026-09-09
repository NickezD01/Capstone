# Phase API Implementation Plan

This plan defines the first implementation slice of the backend restructuring. It covers the new phase entity and phase API only. Task migration and task endpoint refactoring remain a follow-up slice so existing task clients are not broken prematurely.

Implementation status: complete in code. Migration `20260908215306_AddPhases` has been generated but has not been applied to a database. The unique `(ProjectId, Name)` index is filtered to `[IsDeleted] = 0`.

Related documents:

- `BACKEND_RESTRUCTURING_PLAN.md`
- `FRONTEND_RESTRUCTURING_PLAN.md`
- `ROLE_API_ACCESS_MATRIX.md`
- `FRONTEND_API_REFERENCE.md`

## 1. Goals

Implement a first-class phase resource with:

- A project relationship.
- Ordered phases.
- Baseline dates.
- Phase status.
- Optimistic concurrency.
- PM ownership checks.
- Project-scoped read authorization.
- Create, list, detail, update, and cancel endpoints.

The implementation must be additive and migration-safe. Existing `TaskItem.PhaseName` behavior must continue to work until the task migration is implemented.

## 2. Scope

### Included

- `Phase` domain model.
- EF Core configuration and database migration.
- Phase request/response DTOs.
- Phase service and interface.
- Phase controller.
- Dependency-injection registration.
- AutoMapper mapping.
- Unit and EF model tests.
- API documentation updates.

### Not included in this slice

- Removing `TaskItem.PhaseName`.
- Making `TaskItem.PhaseId` required.
- Rewriting task creation/update endpoints.
- AI phase generation and preview confirmation.
- Customer assignment implementation.
- Project budget, supplier, procurement, chat, or inventory restructuring.

## 3. Domain Model

Add `cpms_Domain/Models/Phase.cs` with:

| Field | Type | Rules |
| --- | --- | --- |
| `PhaseId` | `int` | Primary key |
| `ProjectId` | `int` | Required foreign key |
| `Name` | `string` | Required, trimmed, max 200 characters |
| `Description` | `string?` | Optional, max 2,000 characters |
| `SequenceOrder` | `int` | Required, non-negative |
| `BaselineStart` | `DateTime` | Required |
| `BaselineEnd` | `DateTime` | Must be on or after start |
| `Status` | `PhaseStatus` | Defaults to `PLANNED` |
| `RowVersion` | `byte[]` | SQL row-version concurrency token |

Navigation properties:

- `Phase.Project`
- `Phase.Tasks` for the upcoming task relationship
- `Project.Phases`

Initial phase statuses:

- `PLANNED`
- `IN_PROGRESS`
- `COMPLETED`
- `CANCELLED`

Domain behavior:

- `UpdatePlan(...)` rejects empty names, negative sequence values, invalid dates, and edits to completed/cancelled phases.
- `Cancel()` rejects completed or already-cancelled phases.
- A phase cannot be created or edited outside its project baseline dates.
- A phase name must be unique within its project, case-insensitively at the service boundary and with a database uniqueness constraint where supported.

## 4. Persistence and Migration

### 4.1 EF configuration

Add `PhaseConfiguration`:

- Table: `Phases`.
- Primary key: `PhaseId`.
- String conversion for `PhaseStatus`.
- `RowVersion` configured with `IsRowVersion()`.
- Check constraints for sequence and dates.
- Index on `(ProjectId, Name)`.
- Index on `(ProjectId, SequenceOrder)`.
- Query filter for `IsDeleted`.
- Required `Project -> Phases` relationship with cascade delete consistent with the existing project/task model.

### 4.2 Compatibility relationship

Add a nullable `TaskItem.PhaseId` and optional `TaskItem.Phase` navigation in this slice only. Keep `TaskItem.PhaseName` unchanged and required for the current API.

The first migration should:

1. Create `Phases`.
2. Add nullable `TaskItems.PhaseId`.
3. Add the foreign key and index.
4. Avoid backfilling existing tasks automatically in this API slice.

The later task migration will create one `General` phase per existing project, assign legacy tasks, validate the relationship, and then make `PhaseId` required.

Do not delete or rename `PhaseName` in this migration.

## 5. API Contract

### Create phase

`POST /api/Projects/{projectId}/phases`

Authorization: `PM`, with service validation that the PM owns the project.

Request:

```json
{
  "name": "Foundation",
  "description": "Groundwork and foundation activities",
  "sequenceOrder": 1,
  "baselineStart": "2026-09-01T00:00:00Z",
  "baselineEnd": "2026-09-30T00:00:00Z"
}
```

Behavior:

- Project must exist.
- PM must own the project.
- Completed/cancelled projects reject new phases.
- Dates must be inside the project baseline period.
- Duplicate phase names within the project return a validation/conflict response.
- New phases start with `PLANNED`.
- Successful creation returns HTTP 201.

### List project phases

`GET /api/Projects/{projectId}/phases`

Authorization: project-scoped read access.

Return phases ordered by `SequenceOrder`, then `Name`.

### Get phase detail

`GET /api/Phases/{phaseId}`

Authorization: project-scoped read access through the phase's project.

### Update phase

`PUT /api/Phases/{phaseId}`

Authorization: `PM`, with service validation that the PM owns the phase's project.

Request:

```json
{
  "name": "Updated Foundation",
  "description": "Updated scope",
  "sequenceOrder": 1,
  "baselineStart": "2026-09-01T00:00:00Z",
  "baselineEnd": "2026-09-30T00:00:00Z",
  "rowVersion": "AAAAAA=="
}
```

Behavior:

- Reject stale or missing row versions with HTTP 409.
- Revalidate dates against the parent project.
- Reject edits to completed/cancelled phases.
- Revalidate project-local name uniqueness.
- Return the updated phase, including the new row version.

### Cancel phase

`POST /api/Phases/{phaseId}/cancel`

Authorization: `PM`, with service validation that the PM owns the phase's project.

Request:

```json
{
  "rowVersion": "AAAAAA=="
}
```

Behavior:

- Reject stale row versions with HTTP 409.
- Reject completed or already-cancelled phases.
- Return phase ID, status, and new row version.
- Before task migration, cancellation does not delete or rewrite the legacy `PhaseName` on tasks.

## 6. Authorization Design

Authorization must be enforced in the service, not only by controller attributes.

Project-scoped read access should be centralized so phase, task, material, progress, and export APIs do not implement different rules.

For this first slice:

- `ADMIN`: read phases; no phase mutation.
- Owning `PM`: read and mutate phases for managed projects.
- `WAREHOUSE_MANAGER`: read phases only when the project is within the manager's authorized operational scope under the current backend rules.
- `CUSTOMER`: enable read access once nullable `CustomerUserId` and the shared customer project-access policy are implemented.
- `SUPPLIER` and `WORKER`: no phase access.

Recommended follow-up: extract a reusable `IProjectAccessService` or equivalent policy service before customer project visibility is added.

## 7. Application Layer Changes

Add:

- `Request/Phase/CreatePhaseRequest.cs`.
- `Request/Phase/UpdatePhaseRequest.cs`.
- `Request/Phase/PhaseLifecycleRequest.cs`.
- `Response/Phase/PhaseResponse.cs`.
- `Interfaces/IPhaseService.cs`.
- `Services/PhaseService.cs`.

The service should use the existing `IUnitOfWork`, `IClaimService`, `ApiResponse`, and row-version conventions.

Use the existing generic repository pattern initially:

- Add `IGenericRepository<Phase> Phases` to `IUnitOfWork`.
- Construct it in `Infrastructure/UnitOfWork.cs`.
- Use `AppDbContext.Phases` for EF model registration.

If phase-specific query behavior grows later, introduce `IPhaseRepository`; do not add it prematurely.

## 8. Controller and DI Changes

Add `cpms_API/Controllers/PhasesController.cs` with explicit routes:

- `POST /api/Projects/{projectId}/phases`.
- `GET /api/Projects/{projectId}/phases`.
- `GET /api/Phases/{phaseId}`.
- `PUT /api/Phases/{phaseId}`.
- `POST /api/Phases/{phaseId}/cancel`.

Register `IPhaseService` and `PhaseService` in `Program.cs`.

Use the same response wrapper and status-code handling as the existing controllers.

## 9. Validation and Error Contract

Expected responses:

| Situation | Response |
| --- | --- |
| Missing project/phase | 404 |
| Invalid name, dates, or sequence | 400 |
| Caller lacks project access | 403 |
| Closed project/phase cannot be changed | 409 |
| Duplicate phase name | 409 or validation 400, consistently documented |
| Stale row version | 409 |
| Unexpected persistence failure | 500 |

Use field-level validation details where the existing validation pipeline supports them. Do not expose database exception text directly.

## 10. Tests

### Domain tests

- Valid phase plan can be created and updated.
- Empty names are rejected.
- Negative sequence values are rejected.
- Invalid dates are rejected.
- Completed/cancelled phases cannot be edited.
- Completed/already-cancelled phases cannot be cancelled.

### Service tests

- Owning PM can create a phase.
- Non-owning PM receives 403.
- Admin can read but cannot mutate.
- Missing project returns 404.
- Dates outside the project baseline return 400.
- Duplicate names are rejected within one project but allowed in different projects.
- List ordering is sequence then name.
- Stale row version returns 409.
- Closed projects reject phase creation/update/cancellation.

### EF model tests

- `Phases` table mapping exists.
- Project-to-phase relationship is configured.
- Phase name/project uniqueness index exists.
- Task-to-phase relationship is nullable during compatibility migration.
- Phase row version is configured as a concurrency token.

## 11. Documentation Updates

After the API is implemented:

1. Add the implemented routes and DTOs to `FRONTEND_API_REFERENCE.md`.
2. Update `ROLE_API_ACCESS_MATRIX.md` with phase read/write access.
3. Update `FRONTEND_RESTRUCTURING_PLAN.md` if the implemented routes or response fields differ from the target contract.
4. Record the migration name and whether it has been applied to each environment.

Do not document the target routes as production-ready until the controller, migration, and tests are complete.

## 12. Implementation Order

1. Add the domain model and status enum.
2. Add project/phase navigation properties.
3. Add nullable task compatibility relationship.
4. Add EF configuration and `DbSet`.
5. Add generic repository access to the unit of work.
6. Add DTOs, mapping, service, and interface.
7. Add controller routes and DI registration.
8. Add unit and EF model tests.
9. Generate and inspect the EF migration SQL.
10. Build the solution and run the test suite.
11. Update frontend API and role documentation.
12. Only after approval, apply the migration to the intended database.

## 13. Definition of Done

- Phase CRUD/read operations work through the documented routes.
- PM ownership is enforced at the service layer.
- Read access is project-scoped.
- Row-version conflicts are handled consistently.
- Existing task APIs still compile and behave as before.
- Existing `PhaseName` data is preserved.
- The migration is additive and reversible.
- Tests cover domain rules, authorization, ordering, validation, and concurrency.
- Frontend API and role documentation match the implemented contract.
