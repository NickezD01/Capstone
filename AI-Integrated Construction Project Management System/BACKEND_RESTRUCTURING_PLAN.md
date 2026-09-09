# Backend Restructuring Plan

## 1. Purpose

Restructure the backend around the new construction-management workflow and role boundaries:

```text
Project -> Phase -> Task -> Material requirement -> Material request -> Inventory issue
```

The target application roles are:

- `ADMIN`
- `PM` (Project Manager)
- `WAREHOUSE_MANAGER`
- `CUSTOMER`

`SUPPLIER` is not a supported application login role in the target model. Supplier entities may remain as internal reference data for legacy records and optional stock-in source information. Procurement and purchase-order workflows are deprecated and removed from the active model. `WORKER` is not part of the target application role model unless a later requirement brings it back.

The target application also excludes user chat, AI supplier recommendations, and AI chatbot functionality. Existing database tables and historical records may be retained temporarily for migration safety, but new clients must not depend on these features.

This document is an implementation plan and target contract. Existing APIs should be migrated deliberately rather than removed without a compatibility decision.

## 2. Target Operating Model

### Project lifecycle

1. A project manager creates a project, optionally assigning one customer during creation.
2. A project manager may assign or correct the customer assignment while the project is not archived.
3. The project manager creates project phases, either manually or with AI.
4. The project manager creates tasks inside a phase, either manually or with AI.
5. The project manager assigns material requirements to tasks and submits material requests.
6. Each material request records an estimated cost supplied by the project manager. This is a planning value and does not immediately reduce the project budget.
7. The warehouse manager reviews stock, approves or rejects requests, adjusts the actual cost when known, reserves material, and issues material.
8. The actual cost is debited from the project budget when material is issued through an auditable budget transaction. Partial issues create partial debits. Later actual-cost corrections apply only the difference from the amount already debited, and returns create explicit reversal entries.
9. Project progress is visible to the project manager and assigned customer.
10. Authorized users can generate a project Excel workbook for download.

A task must belong to a phase. A phase must belong to a project. Tasks must not use a free-text phase name as their primary relationship.

### Warehouse model

There is exactly one active operational warehouse and one inventory context for the application. The backend should preserve the warehouse schema and historical identifiers for migration safety while preventing creation, selection, or operation of additional warehouses.

The warehouse manager is responsible for:

- Current stock and stock availability.
- Stock-in and stock-out transactions.
- Material-request approval, rejection, reservation, release, and issue.
- Inventory adjustments and physical-count workflows, subject to the existing approval controls.

## 3. Role and Permission Contract

`Read` includes viewing details and status. `Write` includes create, update, delete, approval, lifecycle transitions, inventory mutations, and assignment changes.

| Area | ADMIN | PM | WAREHOUSE_MANAGER | CUSTOMER |
| --- | --- | --- | --- | --- |
| User/account management | Read/write | Read own account | Read own account | Read own account |
| Material/category master data | Read/write | Read | Read | Read |
| Supplier/catalog records | Read/write where retained | Read | Read | No access unless needed for project display |
| Project creation and editing | Read only | Read/write own projects | Read | Read assigned project only |
| Assign customer to project | Read | Write for own projects | Read | No |
| Phase creation/editing | Read | Write for own projects | Read | Read assigned project |
| Task creation/editing | Read | Write for own projects | Read | Read assigned project |
| Project lifecycle/status | Read or administrative override | Write for own projects | Read | Read |
| Project progress/reports | Read | Write/review own projects | Read | Read assigned project only |
| Material requirements | Read | Write for own tasks/projects | Read | Read only if exposed in project details |
| Material requests and costs | Read | Create/update/cancel own project requests with estimated cost | Approve/reject/release/issue and adjust actual cost | Read assigned project status and permitted cost details |
| Inventory and stock transactions | Read | Read only | Read/write |
| AI phase/task generation | Optional read/use for support | Write/use for own projects | No project-plan mutation | No |
| AI Excel generation | Allowed | Allowed for own projects | Allowed for stock/request views if needed | Allowed for assigned project details |

### Authorization rules

- Use the exact JWT role values `ADMIN`, `PM`, `WAREHOUSE_MANAGER`, and `CUSTOMER`.
- Enforce project ownership and customer assignment in the application service, not only in controller attributes.
- A customer may only read projects explicitly assigned to that customer.
- A PM may only mutate projects they manage.
- A warehouse manager may mutate inventory and material requests but may not mutate project, phase, task, material master, or customer-assignment data.
- A warehouse manager may adjust actual material-request cost, but may not change the original estimated cost or project budget directly. Actual-cost changes must go through the budget transaction service.
- Admin write access is limited to user management and material/category master data. Admin project, phase, task, and inventory endpoints are read-only under this target model unless a separate emergency-administration policy is approved.
- Remove broad `Authenticated` access from project-sensitive AI and export endpoints. Authorization must be checked against the referenced project.
- Procurement and purchase-order endpoints are deprecated and must not be used by new clients. New write operations should return `410 Gone` during the migration period. Existing purchase-order records may remain read-only history until a retention decision is made.
- Supplier accounts must not be created or authenticated. Supplier records are internal reference data only.
- Chat, AI supplier recommendation, and AI chatbot endpoints are deprecated. New clients must not create or depend on these features.

## 4. Domain and Persistence Changes

### 4.1 Add a first-class `Phase` entity

Create `Phase` in `cpms_Domain/Models` with at least:

- `PhaseId`
- `ProjectId`
- `Name`
- `Description` or `Notes`
- `SequenceOrder`
- `BaselineStart`
- `BaselineEnd`
- `Status`
- `RowVersion`
- `Project` navigation
- `Tasks` navigation

Recommended constraints:

- Unique phase name within a project, or a documented alternative if duplicate names are required.
- `SequenceOrder` is non-negative and stable for ordering.
- Phase dates must be valid and should normally fall within the project plan dates.
- A closed/cancelled phase cannot be edited except through an explicit reopen policy.

Add `Project.Phases` and configure the relationship in `AppDbContext`.

### 4.2 Change `TaskItem`

Replace `TaskItem.PhaseName` with:

- `PhaseId` as a required foreign key.
- `Phase` navigation.

Keep task-level scheduling, assignment, budget, progress, material requirements, and row-version concurrency behavior. Update task validation and mapping so all task creation and update requests select a phase belonging to the same project.

`PhaseName` may be retained temporarily as a non-persistent compatibility field only if an API migration needs it. It must not remain the source of truth.

### 4.3 Add customer assignment to projects

Add a nullable `CustomerUserId`. Existing projects may have no customer. A project without a customer is visible only to its PM and admins; an assigned customer can view it after assignment.

Add:

- `Project.CustomerUserId`
- `Project.Customer` navigation
- Foreign key to the user-account table
- Index for customer project lookup

Customer assignment must validate that the target account has the `CUSTOMER` role. The owning PM may assign or correct the customer while the project is not archived. Reassignment must be audited and immediately remove the former customer's access.

### 4.4 Enforce one warehouse and one inventory

Choose one canonical active warehouse during migration. Since operational inventory may be recreated, use a controlled migration after a backup. Then:

- Add a database/application invariant allowing only one active operational warehouse.
- Keep `WarehouseId` on inventory and transaction records for historical integrity.
- Remove or disable warehouse-create, warehouse-selection, and warehouse-transfer workflows from the target API.
- Remove warehouse selection from PM material requests and MRP calls where it is no longer meaningful.
- Centralize warehouse resolution in a service such as `IWarehouseContext` or equivalent.
- Keep stock-in and stock-out ledgers immutable and expose current stock through inventory read APIs.

If operational history is intentionally wiped, back up the database first and remove it through a reviewed migration. Do not manually delete rows or remove the warehouse schema.

### 4.5 Material-request cost and project-budget accounting

Extend material requests with cost fields such as:

- `EstimatedCost` - required when the PM creates the request; used for planning and display only.
- `ActualCost` - set or adjusted by the warehouse manager when the real material cost is known.
- `BudgetDebitedAmount` - the amount already posted against the project budget for this request.
- `ActualCostUpdatedAt` and `ActualCostUpdatedByUserId` for auditability.

Cost rules:

- Estimated cost must be non-negative and must be based on the requested or approved quantities and material pricing available to the PM.
- Creating or editing a pending request changes the estimate only; it does not subtract money from the project budget.
- When the warehouse manager issues material, the service must debit the actual cost from the associated project budget in the same transaction as the issue state change where possible.
- If actual cost is adjusted after a debit, post only the delta: `newActualCost - BudgetDebitedAmount`. Never debit the full actual cost twice.
- Rejecting, cancelling, or releasing a request before issue creates no debit. Returns and post-issue corrections require explicit reversal or delta ledger entries; releasing a reservation does not reverse a cost because reservation does not debit the budget.
- A request cannot be issued or finalized if its actual cost is negative, its project is missing, or the resulting project budget would violate the approved budget policy. Partial issue is allowed only when the approved quantity and cost are recorded per line.
- Budget debits and reversals must be immutable ledger entries linked to the material request, project, acting user, old cost, new cost, and timestamp. Project budget summaries should be derived from this ledger rather than overwritten silently.
- Use optimistic concurrency for both the material request and project budget so concurrent warehouse updates cannot double-debit the budget.

## 5. API and Service Restructuring

### Project APIs

Keep or introduce endpoints with these responsibilities:

- `POST /api/Projects` - PM creates a project.
- `GET /api/Projects` and `GET /api/Projects/{id}` - role-filtered reads.
- `PUT /api/Projects/{id}` - PM edits an owned project while editable.
- `PUT /api/Projects/{id}/customer` - PM assigns or corrects one customer while the project is not archived, with role validation and an audit record.
- Existing lifecycle endpoints - PM-owned writes only; admin becomes read-only unless explicitly exempted.

Recommended project lifecycle states are `DRAFT`, `PLANNED`, `ACTIVE`, `PAUSED`, `COMPLETED`, and `CANCELLED`. PMs control lifecycle transitions. Completed and cancelled projects are read-only by default and require an explicit audited reopen action. Completion should be blocked while open tasks or unresolved material requests remain.

### Phase APIs

Introduce a dedicated phase service/controller:

- `POST /api/Projects/{projectId}/phases`
- `GET /api/Projects/{projectId}/phases`
- `GET /api/Phases/{phaseId}`
- `PUT /api/Phases/{phaseId}`
- `POST /api/Phases/{phaseId}/cancel` or the project-approved lifecycle equivalent

Only PMs can create or change phases. Every operation must verify project ownership.

### Task APIs

Refactor task endpoints so task creation uses `PhaseId`:

- `POST /api/Phases/{phaseId}/tasks`
- `GET /api/Projects/{projectId}/tasks`
- `GET /api/Tasks/{taskId}`
- `PUT /api/Tasks/{taskId}`
- Existing task lifecycle endpoints, restricted to the owning PM.

Reject a task when its phase is not part of the referenced project. Preserve optimistic concurrency with `RowVersion`.

### Material-request APIs

Keep the current two-step responsibility split:

- PM creates, updates, and cancels pending requests for an owned project.
- The PM must submit an `EstimatedCost` with the request. The PM may update that estimate only while the request is pending.
- Warehouse manager approves/rejects, reserves/releases, issues material, and adjusts `ActualCost`.
- Actual-cost issue or adjustment creates an auditable project-budget debit or delta adjustment. The client must not directly set `BudgetDebitedAmount`.
- Admin, PM, warehouse manager, and assigned customer receive only the read view appropriate to their scope.

All request creation must resolve the single warehouse server-side. Clients must not choose an arbitrary warehouse.

### Deprecated procurement and purchase-order APIs

Purchase orders and procurement are deprecated because material costs are now managed through material requests and warehouse actual-cost accounting. Do not add new purchase-order functionality.

- Mark purchase-order/procurement endpoints as deprecated in the API contract and documentation.
- Disable purchase-order creation, approval, receiving, shipping, and procurement workflow endpoints for new clients. During migration, deprecated write endpoints should return `410 Gone`.
- Keep existing purchase-order data read-only where required for historical reporting, or hide it behind an administrative migration/reporting endpoint.
- Do not use supplier catalogs or supplier recommendations as a required step in material-request approval.
- Remove purchase-order dependencies from new MRP, material-request, AI, and Excel flows.

The replacement shortage behavior is stock-based: the warehouse manager may reject a request with a shortage reason or approve only the available quantity when partial fulfillment is supported. No purchase order is created.

### Inventory APIs

Warehouse manager write APIs should operate on the canonical warehouse only:

- Stock-in/receipt.
- Stock-out/material issue.
- Return.
- Adjustment request and approval flow.
- Physical count and reconciliation.
- Current stock, transaction history, and availability reads.

PM and customer inventory access is read-only, and only the minimum project/material information needed by their workflows should be returned.

## 6. AI and Excel Features

### AI planning

AI is a productivity feature for the project manager, not an independent authorization path.

The planner should support:

- Generate all phases for an existing PM-owned project.
- Generate general tasks for a selected phase or for generated phases.
- Validate and normalize AI output before persistence.
- Return a preview only; do not create database records during generation.
- Allow the PM to review, edit, remove, or add proposal items before confirmation.
- Create the confirmed phases and tasks in one validated transaction.
- Use temporary preview IDs so proposed tasks can reference proposed phases.
- Preserve the existing strict JSON-only response contract.
- Prevent AI from directly changing inventory, approving requests, assigning customers, or changing material master data.

Recommended API shape:

- `POST /api/Projects/{projectId}/ai/phases:generate`
- `POST /api/Projects/{projectId}/ai/tasks:generate`
- Use explicit preview/confirm endpoints because generation must not persist immediately.

The existing five-question planner can remain as the input experience, but its output should map to `Phase` and `TaskItem` records when the PM confirms it. `phaseId` must be present in task output after phase creation or proposal normalization.

### Excel generation

Use the existing ClosedXML implementation as the export mechanism. Define separate, authorization-aware export views:

- PM: full details for owned projects, including project, phases, tasks, materials, requests, schedule, budget, and progress.
- Customer: the same project workbook as the PM, including project, phases, tasks, material requests, estimated costs, actual costs, budget impact, schedule, and progress. Do not include unrelated projects or user/account administration data.
- Warehouse manager: inventory, stock movement, and material-request details; project fields are read-only context.
- Admin: user/material management and read-only operational reports.

Export endpoints must verify project/customer ownership before generating a file. Avoid accepting an arbitrary serialized plan from an unauthenticated or unrelated user as the authorization boundary.

## 7. Supplier Handling

Keep supplier entities and supplier catalogs only as internal reference data because they support historical procurement/material-source data. They are not application roles and are not part of the active material-request workflow.

Recommended approach:

- Keep supplier records as internal reference data only; they may be admin-managed if historical maintenance is required.
- Make supplier references optional in active stock-in and inventory workflows wherever the existing schema permits it.
- Do not expose supplier capabilities as a user-facing role.
- Disable supplier registration and authentication, and deactivate existing supplier accounts.
- Remove supplier recommendations from the target application.
- Do not remove supplier tables merely to simplify the four-role permission model.

## 8. Migration Sequence

1. Back up the database and freeze the target scope.
2. Inventory role seeds, JWT claims, controllers, services, DTOs, mappings, EF configurations, tests, and frontend API usage.
3. Deprecate supplier login, procurement/purchase orders, chat, AI supplier recommendations, and AI chatbot functionality.
4. Select one canonical warehouse and recreate or migrate operational inventory through a reviewed migration.
5. Add `Phase` and its project relationship.
6. Create one `General` phase for every existing project and assign all existing tasks to it. Preserve unresolved or invalid tasks for review.
7. Validate that every task has a phase, then make `TaskItem.PhaseId` required.
8. Add nullable project customer assignment and customer-filtered project APIs.
9. Update request/response DTOs and mapper profiles from phase name to phase ID and nested phase data where appropriate.
10. Implement the issue-time material budget ledger with partial issue, correction, and reversal behavior.
11. Update authorization policies and service ownership checks.
12. Add AI preview/confirm workflow and PM editing support.
13. Update customer and role-specific export contracts and endpoint authorization.
14. Disable deprecated procurement, supplier, chat, and AI endpoints; return `410 Gone` from deprecated write routes during migration.
15. Remove or deprecate obsolete warehouse-selection, transfer, free-text phase, and overly broad role paths after clients migrate.
16. Update `ROLE_API_ACCESS_MATRIX.md` and `FRONTEND_API_REFERENCE.md` whenever endpoints or DTOs change.

Every migration should have a rollback or backup procedure and a post-migration validation query.

## 9. Tests and Acceptance Criteria

### Domain and database

- A project can have multiple phases and each phase can have multiple tasks.
- A task cannot exist without a valid phase.
- A task cannot reference a phase from another project.
- Phase ordering, date validation, status transitions, and row-version conflicts behave correctly.
- Only one active operational warehouse is available.
- Existing inventory and request history remains queryable.
- Material requests store estimated cost and warehouse-adjusted actual cost.
- Actual cost debits the associated project budget exactly once, with auditable delta adjustments and reversals.

### Authorization

- Admin cannot create/update projects, phases, tasks, or inventory through normal APIs.
- PM can create a project and mutate only owned projects, phases, tasks, requirements, and requests.
- Warehouse manager can mutate inventory and material-request decisions but cannot mutate project plans.
- PM estimates material-request cost, while the warehouse manager adjusts actual cost and cannot bypass the budget ledger.
- Customer can only read assigned project progress and permitted exports.
- Unassigned customers cannot access project details, AI output, or project exports.
- Supplier role, if retained, cannot gain access to the four primary role workflows accidentally.

### AI and export

- AI output is schema-validated before persistence.
- Generated tasks always reference a valid phase.
- AI generation is scoped to the PM's project.
- AI cannot perform inventory or approval mutations.
- Excel files contain the correct role-specific sheets and exclude unauthorized fields.
- PM and customer project exports contain the same approved project, request-cost, budget-impact, and progress details.
- Deprecated purchase-order/procurement endpoints cannot create or advance new procurement workflows.
- Exporting a project that the caller cannot access returns the normal authorization response.

### Regression coverage

Update or add focused tests for:

- `Phase` EF mappings and migration backfill.
- Project-customer assignment.
- PM phase/task ownership.
- Customer project visibility.
- Warehouse single-context behavior.
- Material request approval and issue flow.
- Estimated versus actual material-request cost and exactly-once project-budget debits.
- Actual-cost correction, budget delta, reversal, and concurrency behavior.
- Deprecated purchase-order/procurement endpoint behavior and historical read-only access.
- AI phase/task normalization and persistence.
- Role-specific Excel export.
- Existing inventory concurrency and immutable transaction behavior.
- Deprecated supplier, procurement, chat, AI supplier recommendation, and AI chatbot endpoint behavior.

## 10. Confirmed Target Decisions

- `CustomerUserId` is nullable for legacy and newly created projects. A customer is selected during project creation when applicable. Unassigned projects are visible only to the PM and admins. The PM may correct the assignment while the project is not archived, with an audit record.
- PMs have full control over project lifecycle status. Use `DRAFT`, `PLANNED`, `ACTIVE`, `PAUSED`, `COMPLETED`, and `CANCELLED`, with validation for completion and audited reopening.
- AI phase/task generation returns a preview only. The PM may edit the preview and must explicitly confirm before phases/tasks are persisted.
- Estimated material cost is entered by the PM. Actual material cost is charged to the project budget when material is issued. Partial issues, corrections, and returns use immutable ledger entries and delta/reversal accounting.
- The database retains the warehouse structure, but only one canonical operational warehouse exists. Warehouse selection, creation, and transfers are removed from active workflows. Operational inventory may be recreated after a verified backup.
- Supplier login and supplier-facing functionality are stopped. Supplier entities remain only as internal or historical reference data where needed by the existing schema.
- Procurement and purchase orders are stopped. Existing purchase-order data may remain read-only history, while new procurement write endpoints are deprecated and return `410 Gone` during migration.
- Customers can view assigned project lists, project details, phases, tasks, progress, and the authorized Excel export. They cannot mutate project data.
- User chat, AI supplier recommendations, and AI chatbot functionality are out of scope. Existing records may remain temporarily for migration safety, but no new records or active client dependencies should be created.
