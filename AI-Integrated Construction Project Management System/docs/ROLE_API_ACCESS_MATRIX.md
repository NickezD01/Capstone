# Role API Access Matrix

This file answers one frontend question: after a user is authenticated, which role can access which API?

Source files checked:

- `cpms_API/Controllers/*.cs`
- `cpms_API/Program.cs`
- selected service-level ownership checks in `cpms_Application/Services/*.cs`

## Important Reading Notes

- `Public` means no `[Authorize]` attribute is required by the controller/action.
- `Authenticated` means any logged-in user with a valid JWT can call the API, regardless of role.
- `ADMIN`, `PM`, and `WAREHOUSE_MANAGER` are the main app roles used by controller authorization.
- The domain also has `SUPPLIER`, `CUSTOMER`, and `WORKER`, but almost no protected business APIs explicitly allow these roles. They can only call APIs marked `Authenticated`, plus public APIs.
- Some APIs have extra service-level ownership checks. Example: a `PM` may be allowed by route attribute, but the service may still require that the PM owns the project.
- Role names must match the JWT role claim: `ADMIN`, `PM`, `WAREHOUSE_MANAGER`, `SUPPLIER`, `CUSTOMER`, `WORKER`.

## Frontend Role Guard Summary

Use this as the high-level menu/sidebar rule:

| Area | ADMIN | PM | WAREHOUSE_MANAGER | CUSTOMER | WORKER |
| --- | --- | --- | --- | --- | --- |
| Auth self-service | yes | yes | yes | yes | yes |
| Own profile | yes | yes | yes | yes | yes |
| User/account administration | yes | customer list only | no | no | no |
| Categories read | yes | yes | yes | yes, even public | yes, if logged in |
| Categories write | yes | no | no | no | no |
| Materials read | yes | yes | yes | yes, if logged in | yes, if logged in |
| Materials write | yes | no | no | no | no |
| Projects read | yes | yes | yes | assigned only | no |
| Project context (minimal header) | no | no | no | no | yes (assigned tasks only) |
| Projects create/import/update owned | no (PM-only) | yes | no | no | no |
| Project budget adjustment | no | yes (owning PM only) | no | no | no |
| Project manager reassignment | yes | no | no | no | no |
| Tasks read | yes | yes | yes | yes (assigned projects only) | yes (assigned tasks only) |
| Tasks create/update/lifecycle | no | yes | no | no | no |
| Task assignment | no | yes (self or worker) | no | no | no |
| Progress reports submit | no | yes | no | no | yes (assigned tasks only) |
| Progress reports review | no | yes | no | no | no |
| Progress reports read | yes | yes | yes (linked projects only) | no | yes (assigned tasks only) |
| Material request creation/update/cancel | no | yes | no | no | no |
| Material request approve/reject/issue/release | no | no | yes | no | no |
| Material budget ledger (issue debits, cost corrections, return reversals) | read only | estimate only | yes (must manage the warehouse) | no | no |
| Purchase order writes (create/approve/reject/procure/receive/ship/cancel) | no (retired, HTTP 410) | no (retired, HTTP 410) | no (retired, HTTP 410) | no | no |
| Purchase order reads | yes (history) | yes (history) | yes (history) | no | no |
| Suppliers read | yes | yes | yes | no | no |
| Suppliers write | yes | no | no | no | no |
| Supplier recommendations | no (retired, HTTP 410) | no (retired, HTTP 410) | no (retired, HTTP 410) | no | no |
| Catalog/offers read | yes | yes | yes | no | no |
| Catalog/offers write | yes | no | no | no | no |
| Warehouses read | yes | no | yes | no | no |
| Warehouses create/update | no (both retired, HTTP 410) | no | no | no | no |
| Inventory adjustments/count review | no | no | yes (must manage the warehouse; self-review allowed) | no | no |
| Inventory operations/count creation/returns | no | no | yes (always active warehouse) | no | no |
| Warehouse transfers | read only (writes retired, HTTP 410) | no | read only (writes retired, HTTP 410) | no | no |
| Chat | removed | removed | removed | removed | removed |
| AI Chat | removed | removed | removed | removed | removed |
| AI Construction Planner | yes | yes | yes | no | no |
| Project export | yes (full report) | yes (owned, full) | yes (linked, inventory view) | yes (assigned, full) | no |
| AI project plan preview/confirm | no | yes (owning PM only) | no | no | no |
| Project risks | yes | yes (owned) | no | no | no |
| Meetings | removed | removed | removed | removed | removed |

## Public APIs

These APIs do not require a JWT.

| Method | API | Purpose |
| --- | --- | --- |
| POST | `/api/Auth/register` | Retired — returns HTTP 410. Accounts are created by an administrator. |
| POST | `/api/Auth/login` | Login and receive access/refresh tokens. |
| POST | `/api/Auth/Verification` | Retired — returns HTTP 410. Admin-created accounts are verified automatically. |
| POST | `/api/Auth/resend-verification` | Retired — returns HTTP 410. |
| POST | `/api/Auth/refresh` | Exchange refresh token for a new token pair. |
| POST | `/api/Auth/logout` | Revoke refresh token. |
| POST | `/api/Auth/forgot-password` | Request reset code. |
| POST | `/api/Auth/reset-password` | Reset password with reset code. |
| GET | `/api/Categories` | List material categories. |
| GET | `/api/Categories/{id}` | Get category by id. |
| POST | `/api/Suppliers/recommendations/balanced` | Retired — returns HTTP 410. |

## Authenticated APIs, Any Role

Any logged-in user can call these. That includes `ADMIN`, `PM`, `WAREHOUSE_MANAGER`, `SUPPLIER`, `CUSTOMER`, and `WORKER`.

| Method | API | Notes |
| --- | --- | --- |
| POST | `/api/Auth/change-password` | User changes own password. |
| GET | `/api/UserAccount/GetUserProfile` | Current user's profile. |
| PUT | `/api/UserAccount/UpdateUserProfile` | Current user's profile update. |
| GET | `/api/UserAccount/GetUserId` | Returns current user id. |
| GET | `/api/Materials` | List materials. |
| GET | `/api/Materials/{id}` | Material detail. |
| GET | `/api/Materials/{materialId}/variants` | List variants of a material. |
| GET | `/api/Materials/variants/{variantId}` | Variant detail. |
| POST | `/api/Chat/conversations` | Retired — returns HTTP 410. |
| GET | `/api/Chat/projects/{projectId}/conversations` | Retired — returns HTTP 410. |
| GET | `/api/Chat/conversations/{conversationId}/messages` | Retired — returns HTTP 410. |
| POST | `/api/Chat/conversations/{conversationId}/messages` | Retired — returns HTTP 410. |
| PUT | `/api/Chat/messages/{messageId}` | Retired — returns HTTP 410. |
| DELETE | `/api/Chat/messages/{messageId}` | Retired — returns HTTP 410. |
| PUT | `/api/Chat/conversations/{conversationId}/read` | Retired — returns HTTP 410. |
| POST | `/api/AiChat/sessions` | Retired — returns HTTP 410. |
| GET | `/api/AiChat/sessions` | Retired — returns HTTP 410. |
| GET | `/api/AiChat/sessions/{sessionId}/messages` | Retired — returns HTTP 410. |
| POST | `/api/AiChat/sessions/{sessionId}/messages` | Retired — returns HTTP 410. |
| DELETE | `/api/AiChat/sessions/{sessionId}` | Retired — returns HTTP 410. |
| POST | `/api/Meetings` | Removed. |
| GET | `/api/Meetings/project/{projectId}` | Removed. |
| GET | `/api/Meetings/{meetingId}` | Removed. |
| PUT | `/api/Meetings/{meetingId}/cancel` | Removed. |

## ADMIN APIs

Only `ADMIN` can call these.

| Method | API | Purpose |
| --- | --- | --- |
| POST | `/api/Auth/admin/reset-password/{userId}` | Queue password reset instructions for a user. |
| POST | `/api/UserAccount` | Create a verified account with role and admin-set password. `SUPPLIER` is rejected. |
| GET | `/api/UserAccount/GetAllAccountAsync` | List all accounts. |
| PUT | `/api/UserAccount/UpdateUserRoleProfile/{customerId}` | Change a user's role. |
| GET | `/api/UserAccount/CountUser` | Count users. |
| POST | `/api/Categories` | Create category. |
| PUT | `/api/Categories/{id}` | Update category. |
| DELETE | `/api/Categories/{id}` | Delete category. |
| POST | `/api/Materials` | Create material. |
| PUT | `/api/Materials/{id}` | Update material. |
| DELETE | `/api/Materials/{id}` | Delete material. |
| POST | `/api/Materials/variants` | Create material variant. |
| PUT | `/api/Materials/variants/{variantId}` | Update material variant. |
| DELETE | `/api/Materials/variants/{variantId}` | Delete material variant. |
| POST | `/api/Catalogs` | Add supplier catalog offer. |
| PUT | `/api/Catalogs/{catalogId}` | Update supplier catalog offer. |
| DELETE | `/api/Catalogs/{catalogId}` | Deactivate supplier catalog offer. |
| POST | `/api/Suppliers` | Create supplier. |
| PUT | `/api/Suppliers/{supplierId}` | Update supplier. |
| DELETE | `/api/Suppliers/{supplierId}` | Deactivate supplier. |
| PUT | `/api/Projects/{projectId}/project-manager` | Reassign project manager. |
| POST | `/api/Warehouses` | Create warehouse — retired, returns HTTP 410. |
| PUT | `/api/Warehouses/{warehouseId}` | Update warehouse — retired, returns HTTP 410. |

## PM APIs

Only `PM` can call these by controller attribute.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| POST | `/api/Projects` | Create project. Service requires `PMUserID` to equal the current PM's user id. Optional `customerUserId` must hold the `CUSTOMER` role. |
| PUT | `/api/Projects/{projectId}/customer` | Assign or clear the project customer. Service requires owning PM, non-closed project, and `rowVersion`. |
| POST | `/api/Projects/import-word` | Import project from Word. Service requires PM role/current user. |
| POST | `/api/Projects/import-word-ai` | AI-extract a project + phase/task draft preview from a Word file. Persists nothing; create the project normally, then `confirm` the preview. |
| POST | `/api/Projects/tasks/{taskId}/materials` | Assign planned material requirement to task. Service requires PM manages the project. |
| PUT | `/api/Projects/{projectId}` | Update project. Service requires owning PM and `rowVersion`. |
| POST | `/api/Projects/adjust-budget` | Adjust project budget. Service requires owning PM. |
| POST | `/api/Projects/{projectId}/risks/recommend-actions` | AI-recommended corrective actions for the risk scan. Owning PM only; preview only, never auto-applied. |
| POST | `/api/Projects/{projectId}/start` | Start project. Service requires owning PM. |
| POST | `/api/Projects/{projectId}/pause` | Pause project. Service requires owning PM. |
| POST | `/api/Projects/{projectId}/cancel` | Cancel project. Service requires owning PM. |
| POST | `/api/Projects/{projectId}/reopen` | Reopen project. Service requires owning PM. |
| POST | `/api/Projects/{projectId}/complete` | Complete project. Service requires owning PM. |
| POST | `/api/Phases/{phaseId}/tasks` | Create task under a phase. Service requires owning PM; phase/project must not be completed or cancelled. `assignedToUserId` must be the PM or a `WORKER`. |
| POST | `/api/task` | Deprecated. Returns `410 Gone` pointing to `POST /api/Phases/{phaseId}/tasks`. |
| GET | `/api/Tasks/assigned` | Get tasks assigned to the caller (`PM` or `WORKER`) by service logic. |
| PUT | `/api/Tasks/{taskId}` | Update task. Service requires owning PM, row version, and the target phase must belong to the same project. May reassign to the PM or a `WORKER`. |
| POST | `/api/Tasks/{taskId}/cancel` | Cancel task. |
| POST | `/api/Tasks/{taskId}/reject` | Reject task. |
| POST | `/api/Tasks/{taskId}/reopen` | Reopen task. |
| POST | `/api/ProgressReport` | Submit progress report. Owning PM, or assigned `WORKER` for their own tasks; PM approval still required. |
| POST | `/api/ProgressReport/{reportId}/approve` | Approve progress report. |
| POST | `/api/ProgressReport/{reportId}/reject` | Reject progress report. |
| POST | `/api/ProgressReport/{reportId}/correct` | Correct progress report. |
| POST | `/api/ProgressReport/{reportId}/reverse` | Reverse progress report. |
| POST | `/api/MaterialRequest` | Create material request with PM `estimatedCost` (planning only, never debits). |
| POST | `/api/MaterialRequest/task/{taskId}` | Create material request from task plan. |
| PUT | `/api/MaterialRequest/{requestId}` | Update pending material request, including the estimate. |
| PUT | `/api/MaterialRequest/{requestId}/cancel` | Cancel pending material request. |

## WAREHOUSE_MANAGER APIs

Only `WAREHOUSE_MANAGER` can call these by controller attribute.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| PUT | `/api/MaterialRequest/{requestId}/approve` | Approve/reserve material request. May set per-line actual unit costs. |
| PUT | `/api/MaterialRequest/{requestId}/reject` | Reject material request. Blocked once budget entries are posted. |
| PUT | `/api/MaterialRequest/{requestId}/issue` | Issue reserved material. Posts immutable per-line debits; supports partial quantities and re-issue; blocked when spend would exceed the approved budget. |
| PUT | `/api/MaterialRequest/{requestId}/actual-cost` | Adjust per-line actual costs. Only the delta on outstanding quantity is posted. |
| PUT | `/api/MaterialRequest/{requestId}/release` | Release reservation. Never touches the budget ledger. |
| POST | `/api/PurchaseOrders` | Retired — returns HTTP 410. |
| GET | `/api/PurchaseOrders/shortages` | View procurement shortages. |
| POST | `/api/PurchaseOrders/from-shortages` | Retired — returns HTTP 410. |
| POST | `/api/PurchaseOrders/{poId}/receive` | Retired — returns HTTP 410. |
| POST | `/api/PurchaseOrders/{poId}/ship` | Retired — returns HTTP 410. |
| POST | `/api/PurchaseOrders/{poId}/processing` | Retired — returns HTTP 410. |
| POST | `/api/Warehouses/inventory/adjust` | Request inventory adjustment; always targets the active warehouse. |
| POST | `/api/Warehouses/inventory/return` | Return inventory to the active warehouse. Posts an explicit budget reversal for linked material requests. |
| POST | `/api/Warehouses/physical-counts` | Start physical count for the active warehouse. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/submit` | Submit physical count for the active warehouse. |
| POST | `/api/WarehouseTransfers` | Retired — returns HTTP 410. |
| POST | `/api/WarehouseTransfers/{id}/ship` | Retired — returns HTTP 410. |
| POST | `/api/WarehouseTransfers/{id}/receive` | Retired — returns HTTP 410. |
| PUT | `/api/WarehouseTransfers/{id}/cancel` | Retired — returns HTTP 410. |

## ADMIN + PM APIs

Both `ADMIN` and `PM` can call these.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/Projects/{projectId}/budget-histories` | View budget history. Service requires ADMIN or owning PM. |
| GET | `/api/Projects/{projectId}/risks` | Deterministic delay/risk scan (schedule, work-item, overall, material, progress, budget). Service requires ADMIN or owning PM. Computed on demand, never persisted. |
| PUT | `/api/PurchaseOrders/{id}/approve` | Retired — returns HTTP 410. |
| PUT | `/api/PurchaseOrders/{id}/reject` | Retired — returns HTTP 410. |

## ADMIN + WAREHOUSE_MANAGER APIs

Both `ADMIN` and `WAREHOUSE_MANAGER` can call these.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/Warehouses` | Admin sees all; warehouse manager sees accessible/managed warehouses by service rules. |
| GET | `/api/Warehouses/{id}` | Admin or manager of that warehouse. |
| GET | `/api/Warehouses/{id}/inventory` | Admin or manager of that warehouse. |
| GET | `/api/Warehouses/{warehouseId}/inventory/{variantId}` | Admin or manager of that warehouse. |
| GET | `/api/Warehouses/inventory/adjustments` | Admin all; warehouse manager filtered by managed warehouse in service. |
| GET | `/api/Warehouses/inventory/transactions` | Admin all; warehouse manager limited to managed warehouse when specified/filterable. |
| GET | `/api/Warehouses/physical-counts` | Admin all; warehouse manager only managed warehouses. |
| POST | `/api/Warehouses/inventory/adjustments/{adjustmentId}/approve` | Approve inventory adjustment. Warehouse manager must manage the adjustment's warehouse; self-review is allowed. |
| POST | `/api/Warehouses/inventory/adjustments/{adjustmentId}/reject` | Reject inventory adjustment. Warehouse manager must manage the adjustment's warehouse; self-review is allowed. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/approve` | Approve physical count. Warehouse manager must manage the session's warehouse; self-review is allowed. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/reject` | Reject physical count. Warehouse manager must manage the session's warehouse; self-review is allowed. |
| GET | `/api/WarehouseTransfers` | Admin all; warehouse manager only transfers involving warehouses they manage. |
| GET | `/api/WarehouseTransfers/{id}` | Admin or manager of source/destination warehouse. |
| PUT | `/api/WarehouseTransfers/{id}/approve` | Retired — returns HTTP 410. |
| PUT | `/api/WarehouseTransfers/{id}/reject` | Retired — returns HTTP 410. |

## ADMIN + PM + WAREHOUSE_MANAGER APIs

These are the main shared business read APIs.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/ProgressReport/task/{taskId}` | View progress reports for a task. Warehouse manager requires operational project access. |
| GET | `/api/Catalogs` | List catalog offers. |
| GET | `/api/Catalogs/{catalogId}` | Get catalog offer. |
| GET | `/api/MaterialRequest` | List material requests. |
| GET | `/api/MaterialRequest/{requestId}` | Material request detail. |
| GET | `/api/MaterialRequest/project/{projectId}` | Material requests by project. |
| GET | `/api/Projects` | List projects. PM sees owned projects; CUSTOMER sees only projects assigned to them; warehouse manager sees operationally linked projects. |
| GET | `/api/Projects/{id}` | Project detail. PM requires ownership; CUSTOMER requires that the project's `customerUserId` equals their user id. |
| GET | `/api/Projects/{id}/context` | Minimal project header (`projectId`, name, address, dates) for assigned `WORKER`s. |
| GET | `/api/Projects/{projectId}/material-requirements` | Project material requirements. |
| POST | `/api/Projects/{projectId}/mrp-runs` | Calculate MRP. PM must own project; warehouse manager must manage the active warehouse. Any supplied `warehouseId` is ignored. |
| GET | `/api/Projects/{projectId}/mrp-runs/latest` | Latest MRP run. PM must own project; warehouse manager must manage the active warehouse. Any supplied `warehouseId` is ignored. |
| GET | `/api/PurchaseOrders` | List purchase orders. |
| GET | `/api/PurchaseOrders/{id}` | Purchase order detail. Service checks access to PO. |
| POST | `/api/PurchaseOrders/{poId}/cancel` | Retired — returns HTTP 410. |
| GET | `/api/Suppliers` | List suppliers. |
| GET | `/api/Suppliers/{supplierId}` | Supplier detail. |
| GET | `/api/Projects/{projectId}/tasks` | Tasks by project. Assigned `CUSTOMER` may list tasks of their projects. The legacy `/api/task` aliases were removed. |
| GET | `/api/Tasks/{taskId}` | Task detail. Assigned `WORKER` may read their own tasks. |
| GET | `/api/Projects/{projectId}/material-requirements` | Task/project material requirements. |

## AI Construction Planner APIs

`generate-json` and `generate-excel` allow `ADMIN`, `PM`, and `WAREHOUSE_MANAGER`. `questions` requires any authenticated user.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/AiConstructionPlanner/questions` | Returns the fixed planner questions. Any authenticated user. |
| POST | `/api/AiConstructionPlanner/generate-json` | Generate a construction plan. If `projectId` is supplied, the caller must have staff project access (ADMIN, owning PM, or operationally linked warehouse manager). |
| POST | `/api/AiConstructionPlanner/generate-excel` | Build an Excel workbook from a generated plan. If `projectId` is supplied, the same staff project-access rule applies. |

## Task Issue APIs (Work Problems)

Reported by the owning PM or the assigned site worker; resolved only by the owning PM.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| POST | `/api/Tasks/{taskId}/issues` | Report a work problem (`description`, `photoUrl?`). Owning PM or assigned `WORKER`. |
| GET | `/api/Tasks/{taskId}/issues` | List work problems, newest first. Owning PM, assigned `WORKER`, or ADMIN. |
| PUT | `/api/Tasks/issues/{issueId}/resolve` | Resolve a work problem (`resolutionNote?`, `rowVersion`). Owning PM only; open issues only. |

## AI Project Planning APIs (Preview/Confirm)

Only the owning `PM` may call these. Generation returns a stateless preview with temporary IDs and persists nothing; confirm persists the PM-edited proposal in a single transaction. Only phases and tasks are created.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| POST | `/api/Projects/{projectId}/ai/phases:generate` | Generate phase proposals from the five planning answers. Owning PM only. |
| POST | `/api/Projects/{projectId}/ai/tasks:generate` | Generate task proposals for one existing `phaseId`, or for the `phases` preview echo (mapped by stable `aiKey`). Owning PM only. |
| POST | `/api/Projects/{projectId}/ai/confirm` | Persist the edited proposal. Every task must reference exactly one of `phaseTempId` or an existing `phaseId`. Validates names, baselines, closed phases, and the project budget cap. Closed projects return HTTP 409. |
| POST | `/api/Projects/{projectId}/ai/complete` | Preview only the remaining phases/tasks from the brief plus current project state. Duplicates are filtered with reasons in `warnings`. Owning PM only. |

## Project Export API

`GET /api/Projects/{projectId}/export` returns a role-specific Excel workbook built from live project data (never from a client-supplied plan). `ADMIN`, owning `PM`, and assigned `CUSTOMER` receive the full view; an operationally linked `WAREHOUSE_MANAGER` receives the inventory view. Anyone else receives HTTP 403.

| View | Sheets |
| --- | --- |
| Full (ADMIN, owning PM, assigned CUSTOMER) | Project, Phases, Tasks, Gantt, Material Requests, Request Lines, Budget Ledger, Budget Summary, Progress |
| Inventory (linked WAREHOUSE_MANAGER) | Project, Material Requests, Request Lines, Inventory, Stock Movements |

Rules: the customer workbook is identical to the PM workbook (same request costs, budget impact, and progress). The inventory view covers only variants tied to the project's requests at the active warehouse (latest 500 movements). No user/account administration data is included in any view.

## APIs Not Intended for CUSTOMER, SUPPLIER, WORKER

`SUPPLIER` and `CUSTOMER` exist in the domain enum, but the controller attributes do not grant them access to the main project/procurement/warehouse/user-admin APIs. `WORKER` has its own narrow grants (see below).

`CUSTOMER` has four exceptions, all scoped by service logic to projects where `customerUserId` equals the caller's user id: `GET /api/Projects` and `GET /api/Projects/{id}`; `GET /api/Projects/{projectId}/export`, which returns the assigned project's full workbook (identical to the PM view); and the read-only lists `GET /api/Projects/{projectId}/phases` and `GET /api/Projects/{projectId}/tasks`.

`WORKER` exceptions (all scoped to tasks assigned to the caller): `GET /api/Tasks/assigned`, `GET /api/Tasks/{taskId}`, `GET /api/Projects/{id}/context` (minimal header: name, address, dates), `POST /api/ProgressReport` (submit for assigned tasks), and `GET /api/ProgressReport/task/{taskId}` (reports of assigned tasks).

They cannot access APIs restricted to:

- `ADMIN`
- `PM`
- `WAREHOUSE_MANAGER`
- `ADMIN,PM`
- `ADMIN,WAREHOUSE_MANAGER`
- `ADMIN,PM,WAREHOUSE_MANAGER` (except the customer-scoped project read routes noted above)

They can access:

- Public auth/category endpoints.
- Own profile endpoints.
- Authenticated material read endpoints.

## User List API Example

The user list/count/role APIs are admin-only:

| Method | API | Allowed role |
| --- | --- | --- |
| GET | `/api/UserAccount/GetAllAccountAsync` | `ADMIN` only |
| GET | `/api/UserAccount/Customers` | `ADMIN`, `PM` — customers only (`id`, name, email), optional `?search=` |
| GET | `/api/UserAccount/Workers` | `ADMIN`, `PM` — site workers only (`id`, name, email), optional `?search=` |
| GET | `/api/UserAccount/CountUser` | `ADMIN` only |
| PUT | `/api/UserAccount/UpdateUserRoleProfile/{customerId}` | `ADMIN` only (`SUPPLIER` can no longer be assigned) |
| POST | `/api/Auth/admin/reset-password/{userId}` | `ADMIN` only |

So for frontend:

- Show user list pages only to `ADMIN`.
- Show user-count dashboards only to `ADMIN`.
- Hide role-management actions from `PM`, `WAREHOUSE_MANAGER`, `CUSTOMER`, `SUPPLIER`, and `WORKER`.

## Phase APIs (Implemented)

Phase endpoints are now available as the first additive restructuring slice. The task-to-phase migration is complete: `TaskItem.PhaseId` is required and every task belongs to a phase. `TaskItems.PhaseName` is retained as a denormalized display copy synced from `Phase.Name` during create/update. `TaskResponse` exposes `phaseId`, `phaseName` (derived from `Phase.Name`), and a nested `phase` summary. Tasks are created under a phase via `POST /api/Phases/{phaseId}/tasks`; the legacy `POST /api/task` returns `410 Gone`.

| Method | API | Allowed role | Service-level rule |
| --- | --- | --- | --- |
| POST | `/api/Projects/{projectId}/phases` | `PM` | PM must own the project; project must not be completed or cancelled |
| GET | `/api/Projects/{projectId}/phases` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | Admin can read all; PM must own the project; warehouse manager must have operational project access; assigned customer may list phases of their projects |
| GET | `/api/Phases/{phaseId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | Access is checked through the phase's project |
| PUT | `/api/Phases/{phaseId}` | `PM` | PM must own the phase's project; row version is required |
| POST | `/api/Phases/{phaseId}/cancel` | `PM` | PM must own the phase's project; row version is required |

Phase request/response models:

- `CreatePhaseRequest`: `name`, `description?`, `sequenceOrder`, `baselineStart`, `baselineEnd`.
- `UpdatePhaseRequest`: the create fields plus `rowVersion`.
- `PhaseLifecycleRequest`: `rowVersion`.
- `PhaseResponse`: `phaseId`, `projectId`, `name`, `description?`, `sequenceOrder`, `baselineStart`, `baselineEnd`, `status`, `createdDate`, `rowVersion`.

Phase status values are `PLANNED`, `IN_PROGRESS`, `COMPLETED`, and `CANCELLED`. Phase names are unique within a project. Dates must remain within the project baseline, and stale updates return HTTP 409.
