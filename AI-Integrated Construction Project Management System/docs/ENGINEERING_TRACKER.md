# Engineering Tracker

Use this file as the lightweight project harness for future implementation. Keep it updated when a workflow changes.

## Current Build

- Last checked: 2026-06-30
- Command: `dotnet build "AI-Integrated Construction Project Management System/AI-Integrated Construction Project Management System.slnx"`
- Result: passing with warnings
- Known warnings:
  - `AutoMapper` high severity advisory
  - `MailKit` moderate severity advisory
  - `MimeKit` moderate severity advisory

## Architecture Snapshot

| Layer | Current purpose |
| --- | --- |
| `cpms_API` | Controllers, middleware, DI, auth setup |
| `cpms_Application` | Services, interfaces, DTOs, repositories contracts |
| `cpms_Domain` | Scaffolded EF entities plus older domain models still present |
| `cpms_Infrastructure` | EF `AppDbContext`, repository implementations, unit of work |

## Endpoint Coverage Tracker

| Capability | Actor | Current endpoint? | Status |
| --- | --- | --- | --- |
| Login/register | All | Yes | Uses scaffolded `User`; first registered user becomes `Admin`, later public registrations become `Customer` |
| Manage users | Admin | Partial | Admin can create users, get user by id, list users, count users, and update roles. Account disable/status needs a database column. |
| Create project | Admin | Yes | Needs admin-only authorization |
| View assigned project | Project Manager | No | Missing assignment rules |
| View own project progress | Customer | No | Missing assignment and customer-only endpoint |
| Manage materials | Warehouse Manager | Partial | Create/list only |
| Manage suppliers | Warehouse Manager | Partial | Create/list only |
| Manage warehouses | Warehouse Manager | Partial | Create/list/inventory only |
| Create material request | Project Manager | No | Tables exist |
| Approve material request | Warehouse Manager | No | Tables exist |
| Issue materials to project | Warehouse Manager | No | Tables exist |
| Create purchase order | Warehouse Manager | Yes | Needs authorization and DTO cleanup |
| Receive supplier goods | Warehouse Manager | No | Tables exist |
| Inventory report | Warehouse Manager | Partial | Basic warehouse inventory endpoint exists |

## Backlog

### P0: Must Fix Before Feature Growth

- [x] Choose one user model: `User` or `UserAccount`.
- [x] Normalize roles to `Admin`, `WarehouseManager`, `ProjectManager`, `Customer`.
- [x] Update JWT role claims and controller authorization.
- [x] Add admin-only user creation.
- [x] Add admin-only user lookup by id.
- [ ] Remove or isolate legacy models that are no longer mapped.
- [ ] Add account status support to the database, such as `IsActive` or `Status`, before implementing disable/enable endpoints.
- [ ] Stop using purchase order import as goods receipt.

### P1: Core Workflow Implementation

- [ ] Add `MaterialRequestService`.
- [ ] Add `MaterialRequestsController`.
- [ ] Add `MaterialIssueService`.
- [ ] Add `MaterialIssuesController`.
- [ ] Add `GoodsReceiptService`.
- [ ] Add `GoodsReceiptsController`.
- [ ] Add project assignment and customer assignment.
- [ ] Add project progress read model for customers.

### P2: Quality And Maintainability

- [ ] Add workflow status constants or enums.
- [ ] Add validators for create/update commands.
- [ ] Add DTOs for all responses.
- [ ] Add transaction helpers for inventory movement.
- [ ] Add unit tests for status transitions.
- [ ] Add integration tests for inventory increase/decrease.

## Workflow Checklists

### Material Request

- [ ] Project manager creates request with details.
- [ ] Request validates project assignment.
- [ ] Request validates positive quantities.
- [ ] Warehouse manager approves or rejects.
- [ ] Approved request can become material issue.

### Material Issue

- [ ] Warehouse manager creates issue from approved request.
- [ ] System checks stock availability.
- [ ] Posting issue deducts inventory.
- [ ] Request status updates after issue.
- [ ] Cancellation/reversal rule is documented.

### Purchase Order

- [ ] Warehouse manager creates PO with details.
- [ ] Total amount calculated server-side.
- [ ] PO can be approved.
- [ ] Approved PO can be received.
- [ ] PO status reflects receipt progress.

### Goods Receipt

- [ ] Warehouse manager creates receipt from approved PO.
- [ ] Receipt validates remaining ordered quantity.
- [ ] Posting receipt increases inventory.
- [ ] PO status updates after receipt.
- [ ] Receipt can be audited by supplier, warehouse, material, and date.

## Suggested Service Names

- `IUserManagementService`
- `IProjectAssignmentService`
- `IMaterialRequestService`
- `IMaterialIssueService`
- `IPurchaseOrderService`
- `IGoodsReceiptService`
- `IInventoryMovementService`

## Definition Of Done For A Workflow

- Controller endpoints exist and are role-gated.
- Request and response DTOs exist.
- Service validates status transitions.
- Service uses one transaction for header, details, and inventory effects.
- Build passes.
- At least one happy-path test and one invalid-transition test exist.
