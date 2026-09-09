# Frontend Restructuring Plan

This document translates the backend restructuring plan into frontend work. It is the implementation handoff for the frontend team and should be kept synchronized with:

- `BACKEND_RESTRUCTURING_PLAN.md`
- `ROLE_API_ACCESS_MATRIX.md`
- `FRONTEND_API_REFERENCE.md`

The backend restructuring plan is the source of truth for business rules. This document describes how those rules should appear in the frontend.

## 1. Target Roles

The supported application roles are:

- `ADMIN`
- `PM`
- `WAREHOUSE_MANAGER`
- `CUSTOMER`

`SUPPLIER` is not a supported login role. `WORKER` is not part of the target application role model.

The frontend must not display navigation or actions for unsupported roles. The backend remains the final authorization boundary; hiding a button is not a security control.

## 2. Project Visibility and Customer Assignment

`CustomerUserId` is nullable because existing projects may not have a customer.

| User | Project visibility |
| --- | --- |
| PM | Projects they manage |
| ADMIN | All projects, read-only under the target model |
| WAREHOUSE_MANAGER | Operational project context required for warehouse workflows |
| CUSTOMER | Projects explicitly assigned to that customer only |

Frontend behavior:

- Add an optional customer selector to the PM project-creation form.
- Show `Unassigned` when `customerUserId` is null.
- Allow the owning PM to assign or correct the customer while the project is not archived.
- Refresh project access immediately after reassignment.
- Do not show unassigned projects in a customer account.
- Do not assume that a customer can access a project merely because they know its ID.
- Display a reassignment confirmation because the previous customer loses access immediately.

Recommended project screens:

- PM/admin project list.
- Customer assigned-project list.
- Project overview.
- Customer assignment editor for PMs.
- Project phases and tasks.
- Project progress and schedule.
- Project materials and material-request summary.
- Excel export action.

## 3. Project Lifecycle

The target project statuses are:

```text
DRAFT -> PLANNED -> ACTIVE -> COMPLETED
          |          |
          v          v
      CANCELLED    PAUSED -> ACTIVE
```

Frontend rules:

- PM controls project status transitions.
- `DRAFT` allows project-plan editing.
- `PLANNED` means the plan is ready but execution has not started.
- `ACTIVE` means work is in progress.
- `PAUSED` can return to `ACTIVE`.
- `COMPLETED` and `CANCELLED` are read-only by default.
- Reopen must be presented as an explicit action and should show the reason/audit prompt required by the backend.
- Hide or disable completion when open tasks or unresolved material requests remain.
- Always use the latest `rowVersion` for lifecycle actions and handle HTTP 409 conflicts.

Do not implement project status as a free-form dropdown. Use the transition actions supported by the backend.

## 4. Phases and Tasks

The new hierarchy is:

```text
Project -> Phase -> Task
```

Every task must have a valid `phaseId` belonging to the same project.

Frontend changes:

- Replace free-text `phaseName` task input with a phase selector or phase-scoped task form.
- Prefer phase-scoped task creation from the project phase screen.
- Show phase sequence, dates, status, tasks, progress, and material requirements together.
- Do not allow a task from one project to be assigned to a phase in another project.
- Preserve `rowVersion` on phase and task edits.
- Provide manual phase/task creation even when AI planning is available.

Migration behavior:

- Existing projects receive a `General` phase.
- Existing tasks are assigned to that phase.
- The frontend should not assume that an old project has meaningful historical phases.
- If the API identifies unresolved migration data, show a review state instead of silently discarding tasks.

Indicative target routes:

| Method | Route | Frontend use |
| --- | --- | --- |
| POST | `/api/Projects/{projectId}/phases` | Create phase |
| GET | `/api/Projects/{projectId}/phases` | List project phases |
| PUT | `/api/Phases/{phaseId}` | Edit phase |
| POST | `/api/Phases/{phaseId}/tasks` | Create task in phase |
| GET | `/api/Projects/{projectId}/tasks` | List project tasks |
| PUT | `/api/Tasks/{taskId}` | Edit task |

The final route and DTO names must be copied into `FRONTEND_API_REFERENCE.md` when the backend endpoints are implemented.

## 5. AI Planning

AI planning is a PM productivity tool. It is not an authorization path and must not directly mutate inventory, budgets, customers, or approvals.

Required flow:

1. PM requests generated phases or tasks.
2. Frontend displays the returned preview.
3. PM edits, removes, or adds preview items.
4. PM explicitly confirms the preview.
5. Frontend sends the edited preview to the confirmation endpoint.
6. Backend validates and persists the confirmed phases/tasks in one transaction.

Important frontend rules:

- Generation must not immediately add records to the project view.
- Use temporary preview IDs to connect proposed tasks to proposed phases.
- Provide Cancel, Regenerate, Edit, and Confirm actions.
- Prevent duplicate confirmation submissions.
- Refresh the project after confirmation.
- Handle expired or stale previews with a clear retry message.
- Keep AI-generated content visibly distinguishable until confirmed.

Indicative target routes:

| Method | Route | Frontend use |
| --- | --- | --- |
| POST | `/api/Projects/{projectId}/ai/phases:generate` | Generate phase preview |
| POST | `/api/Projects/{projectId}/ai/tasks:generate` | Generate task preview |
| POST | `/api/Projects/{projectId}/ai/plan:confirm` | Confirm edited preview |

These are target routes; use the implemented backend contract once available.

## 6. Material Requests and Budget Display

The responsibility split is:

| Action | PM | WAREHOUSE_MANAGER |
| --- | --- | --- |
| Create request | Yes | No |
| Edit estimated cost while pending | Yes | No |
| Cancel pending request | Yes | No |
| Approve/reject | No | Yes |
| Reserve/release | No | Yes |
| Set or correct actual cost | No | Yes |
| Issue material | No | Yes |

Cost behavior:

- `EstimatedCost` is entered by the PM and is planning information only.
- `ActualCost` is maintained by the warehouse manager.
- The project budget is charged when material is issued, not when it is merely requested or reserved.
- Partial issue may create a partial budget debit.
- Corrections create delta entries.
- Returns create reversal entries.
- `BudgetDebitedAmount` is read-only to the frontend.

Frontend display should distinguish clearly between:

- Estimated cost.
- Actual cost.
- Issued quantity.
- Reserved quantity.
- Remaining quantity.
- Budget debited amount.
- Budget impact from corrections or returns.

The PM must not see controls for setting actual cost or budget-debit values. The warehouse manager must not see controls for editing project plans.

## 7. Single-Warehouse Behavior

The target application has one operational warehouse.

Frontend changes:

- Remove warehouse selectors from PM material-request and MRP forms.
- Do not send arbitrary `warehouseId` values for active workflows.
- Remove warehouse creation, warehouse switching, and warehouse-transfer screens.
- Warehouse-manager screens should show the canonical warehouse context as read-only information.
- Keep historical warehouse fields only when required to display legacy records.
- Do not build new UI around multiple warehouses.

The backend resolves the canonical warehouse server-side.

## 8. Excel Export

PM and customer project exports use the same approved project workbook content:

- Project information.
- Customer assignment where permitted.
- Phases and tasks.
- Materials and material requests.
- Estimated and actual material costs.
- Budget impact.
- Schedule and progress.

Exports must be scoped:

- PM: projects they manage.
- CUSTOMER: assigned projects only.
- WAREHOUSE_MANAGER: inventory and material-request views as authorized.
- ADMIN: read-only operational and management views.

Do not include unrelated projects, account administration data, active procurement workflows, or supplier-login information. Show a loading state while the file is generated and handle authorization failures without exposing file contents.

## 9. Features Removed from the Target Frontend

Remove navigation, screens, API clients, state stores, and new write calls for:

- Supplier login and supplier-facing workflows.
- AI supplier recommendations.
- Procurement workflows.
- Purchase-order creation, approval, receiving, shipping, and cancellation.
- User chat.
- AI chatbot.

During migration, deprecated write endpoints may return HTTP `410 Gone`. The frontend should show a feature-unavailable message or remove the action entirely rather than retrying.

Historical supplier and purchase-order information may remain visible only where the backend explicitly exposes it for reporting. It is not part of the active workflow.

## 10. Error and Compatibility Handling

The frontend must support the following backend responses:

- `401`: refresh the session or redirect to login.
- `403`: show that the user is not authorized for the project or action.
- `404`: show that the project/resource is unavailable or no longer visible.
- `409`: refresh the resource and report a concurrent update conflict; do not silently overwrite changes.
- `410`: remove or mark the deprecated feature as unavailable.
- `422` or validation `400`: display field-level validation errors.

For mutation requests:

- Preserve and send the latest `rowVersion`.
- Prevent duplicate submissions while a request is pending.
- Refresh relevant project, task, request, and budget data after success.
- Never calculate or submit authoritative budget totals from the client.

## 11. Frontend Implementation Order

1. Update role guards and remove unsupported-role navigation.
2. Add nullable customer assignment and customer project visibility.
3. Add project status transition UI.
4. Add phase screens and replace task `phaseName` with `phaseId`.
5. Add the `General`-phase migration display behavior.
6. Implement AI preview/edit/confirm flow.
7. Update material-request screens for estimated versus actual cost.
8. Remove warehouse selectors and multi-warehouse screens.
9. Add customer project detail and Excel export.
10. Remove supplier, procurement, purchase-order, chat, AI supplier recommendation, and AI chatbot features.
11. Update API clients, DTOs, route guards, tests, and error handling.
12. Update `ROLE_API_ACCESS_MATRIX.md` and `FRONTEND_API_REFERENCE.md` with the implemented routes and response contracts.

## 12. Frontend Acceptance Checklist

- Customers see only explicitly assigned projects.
- Unassigned projects are invisible to customers.
- PMs can assign or correct a customer on non-archived projects.
- Existing tasks display under the migrated `General` phase.
- Tasks cannot be created without a valid project phase.
- AI generation does not persist records before confirmation.
- PMs can edit AI previews before confirmation.
- Estimated cost and actual cost are visually distinct.
- Budget impact updates after material issue, correction, and return.
- No active workflow requires a warehouse selector.
- PM and customer exports contain the same authorized project data.
- Deprecated feature screens and API calls are removed.
- `401`, `403`, `404`, `409`, `410`, and validation errors have defined UI behavior.
