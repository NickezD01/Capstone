# Implementation Plan

Last updated: 2026-06-30

## Guiding Principles

- Keep database-first models scaffolded in `cpms_Domain/Models` and `cpms_Infrastructure/AppDbContext.cs`.
- Put business rules in `cpms_Application` services, not controllers.
- Controllers should handle HTTP shape, authorization, and response codes only.
- Every inventory movement must have a document trail.
- Every role-gated endpoint must use the same role names.

## Phase 1: Identity And Role Alignment

Goal: make the four actors real and enforceable.

Tasks:

- Decide whether the application will use scaffolded `User` or legacy `UserAccount`.
- Prefer converging on scaffolded `User` if it matches the database.
- Replace role names with one shared source:
  - `Admin`
  - `WarehouseManager`
  - `ProjectManager`
  - `Customer`
- Update JWT claims to emit the same role values used by `[Authorize(Roles = "...")]`.
- Replace current `Manager` authorization usage.
- Add admin-only endpoints for user list, role update, and account status.
- Add seed or setup path for first admin account.

Current status:

- User APIs now use scaffolded `User`.
- JWT role claims use the normalized role values.
- `UserAccountController` admin endpoints now require `Admin`.
- First public registration becomes `Admin`; later public registrations become `Customer`.
- Admin can create users with any valid role.
- Admin can fetch users by id, list users, count users, and update roles.
- Remaining cleanup: remove/isolate legacy `UserAccount` domain model and add account status fields, such as `IsActive` or `Status`, if disable/enable account behavior is needed.

Acceptance:

- Admin can manage users.
- Non-admin cannot manage users.
- Warehouse manager cannot create users or projects unless explicitly allowed.
- Customer token cannot access warehouse or admin endpoints.

## Phase 2: Project Ownership And Customer Visibility

Goal: make projects belong to the right people.

Tasks:

- Add or confirm relationships for project manager assignment and customer assignment.
- Add admin endpoint to create project and assign project manager/customer.
- Add project manager endpoint to view assigned projects.
- Add customer endpoint to view only assigned project progress.
- Add project progress endpoints over `Tasks` and `ProgressReports`.
- Decide whether progress is task-based, project-level, or both.

Acceptance:

- Admin can create projects.
- Project manager sees only assigned projects.
- Customer sees only assigned projects and read-only progress.
- Unauthorized project IDs return not found or forbidden consistently.

## Phase 3: Material Request Workflow

Goal: project manager can request materials from warehouse.

Suggested endpoints:

- `POST /api/material-requests`
- `GET /api/material-requests`
- `GET /api/material-requests/{id}`
- `PUT /api/material-requests/{id}/submit`
- `PUT /api/material-requests/{id}/approve`
- `PUT /api/material-requests/{id}/reject`

Rules:

- Project manager creates requests for assigned projects.
- Request detail rows must reference valid materials and positive quantities.
- Warehouse manager approves or rejects submitted requests.
- Approved requests become eligible for material issue.

Acceptance:

- Request totals and details persist in one transaction.
- Request status transitions are validated.
- Project manager cannot request materials for someone else's project.

## Phase 4: Material Issue Workflow

Goal: warehouse manager delivers material to project and inventory decreases.

Suggested endpoints:

- `POST /api/material-issues/from-request/{requestId}`
- `GET /api/material-issues`
- `GET /api/material-issues/{id}`
- `POST /api/material-issues/{id}/post`
- `POST /api/material-issues/{id}/cancel`

Rules:

- Material issue should usually reference an approved material request.
- Posting an issue deducts `MaterialInventory`.
- Insufficient stock must stop posting.
- Cancelled posted documents should require a reversing transaction or explicit reversal document.

Acceptance:

- Posting is atomic: all stock deductions succeed or none do.
- Request status becomes `ISSUED` after full issue.
- Partial issue behavior is explicitly decided before coding.

## Phase 5: Purchase Order Workflow

Goal: warehouse manager buys material from supplier.

Current state:

- Basic purchase order create/list/approve exists.
- Details use `PurchaseOrderDetail`.
- Import currently updates inventory directly.

Tasks:

- Restrict purchase order endpoints to warehouse manager.
- Replace `ImportToWarehouseAsync` with goods receipt flow.
- Add clear status transitions:
  - `DRAFT` -> `SUBMITTED` -> `APPROVED` -> `PARTIALLY_RECEIVED` or `RECEIVED`
- Keep total amount calculation in service.
- Include details in get-by-id responses.

Acceptance:

- Approved PO can be received through goods receipt.
- PO cannot be edited after approval unless explicitly reopened.
- PO received quantities can be compared against ordered quantities.

## Phase 6: Goods Receipt Workflow

Goal: supplier delivery to warehouse is recorded and inventory increases.

Suggested endpoints:

- `POST /api/goods-receipts/from-purchase-order/{poId}`
- `GET /api/goods-receipts`
- `GET /api/goods-receipts/{id}`
- `POST /api/goods-receipts/{id}/post`
- `POST /api/goods-receipts/{id}/cancel`

Rules:

- Goods receipt must reference an approved purchase order.
- Detail quantities cannot exceed remaining unreceived quantities unless over-receipt is explicitly allowed.
- Posting a goods receipt increases `MaterialInventory`.
- Purchase order status should update after receipt.

Acceptance:

- Receipt is atomic: all inventory increases succeed or none do.
- PO status reflects receipt state.
- Warehouse inventory report shows received stock.

## Phase 7: API Hardening And Tests

Tasks:

- Add request DTOs and response DTOs for every workflow.
- Stop returning EF entities directly from controllers.
- Add FluentValidation validators for all commands.
- Add service tests for status transitions and inventory updates.
- Add authorization tests for all actor boundaries.
- Add integration smoke tests for the four document workflows.

Acceptance:

- `dotnet build` succeeds.
- Workflow tests cover happy path and invalid transitions.
- Every role has a documented endpoint matrix.
