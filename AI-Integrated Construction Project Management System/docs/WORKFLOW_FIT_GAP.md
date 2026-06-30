# Construction Management Workflow Fit/Gap

Last reviewed: 2026-06-30

## Target Product Shape

The application should support four actors:

| Actor | Primary responsibility |
| --- | --- |
| Admin | Manage users and create projects |
| Warehouse Manager | Manage warehouse stock, purchase orders, goods receipts, material issues, materials, suppliers, and warehouses |
| Project Manager | Manage assigned projects, request materials from warehouse, update project progress |
| Customer | Log in and view only their project progress |

## Current Fit Summary

| Area | Current fit | Notes |
| --- | --- | --- |
| Database scaffold | Partial fit | The scaffold now includes `Projects`, `Tasks`, `ProgressReports`, `Materials`, `MaterialInventories`, `MaterialRequests`, `MaterialIssues`, `PurchaseOrders`, `GoodsReceipts`, suppliers, warehouses, and users. |
| Actor model | Partial fit | User APIs now use the scaffolded `User` table and normalized role strings: `Admin`, `WarehouseManager`, `ProjectManager`, `Customer`. Older `UserAccount` model files still exist in the domain folder and should be removed or isolated later. |
| Admin workflow | Partial fit | Admin-only user create, get-by-id, list, count, and role update endpoints exist. Account disable/enable needs a database status column. Project creation still needs admin-only authorization in a later pass. |
| Project workflow | Partial fit | Project read/create exists. Progress models exist. There is no clear project assignment, customer-project visibility, or project manager material request endpoint. |
| Warehouse workflow | Partial fit | Warehouse, material, supplier, catalog, purchase order, and inventory endpoints exist. Material request, material issue, and goods receipt flows are not implemented as first-class services/controllers. |
| Customer workflow | Missing | There is no customer-specific endpoint that restricts project progress visibility to assigned customer projects. |
| Inventory movements | Partial fit | Purchase order import currently increases `MaterialInventory` directly. Goods receipt tables exist but are not used, so the audit trail is incomplete. Material issue inventory deduction is missing. |

## Current Tables That Match The Intended Warehouse Documents

| Workflow document | Header table | Detail table | Current implementation |
| --- | --- | --- | --- |
| Material request | `MaterialRequest` | `MaterialRequestDetail` | Tables exist, service/controller missing |
| Material issue | `MaterialIssue` | `MaterialIssueDetail` | Tables exist, service/controller missing |
| Purchase order | `PurchaseOrder` | `PurchaseOrderDetail` | Basic create/list/approve/import exists |
| Goods receipt | `GoodsReceipt` | `GoodsReceiptDetail` | Tables exist, service/controller missing |

## Key Risks In Current Shape

1. Legacy `UserAccount` model files still exist beside the scaffolded `User` model and should be removed or isolated later.
2. Non-user controllers still need role gates aligned to `Admin`, `WarehouseManager`, `ProjectManager`, and `Customer`.
3. Purchase order import bypasses `GoodsReceipt`, which means supplier-to-warehouse delivery has no proper receiving record.
4. Material issue is missing, so warehouse-to-project delivery does not deduct inventory.
5. Customer access control is missing, so project progress visibility cannot yet be trusted.
6. Old migration/configuration artifacts remain in the project even though the current mapping is database-first in `AppDbContext`.

## Recommended Domain Status Values

Use explicit string constants or enums in application code, even if the database stores strings.

### Project

- `PLANNING`
- `ACTIVE`
- `ON_HOLD`
- `COMPLETED`
- `CANCELLED`

### Material Request

- `DRAFT`
- `SUBMITTED`
- `APPROVED`
- `REJECTED`
- `ISSUED`
- `CANCELLED`

### Material Issue

- `DRAFT`
- `POSTED`
- `CANCELLED`

### Purchase Order

- `DRAFT`
- `SUBMITTED`
- `APPROVED`
- `PARTIALLY_RECEIVED`
- `RECEIVED`
- `CANCELLED`

### Goods Receipt

- `DRAFT`
- `POSTED`
- `CANCELLED`

## Fit Decision

The project is a usable starting point for the described construction management workflow, but it is not yet aligned end to end. The database has most of the right nouns. The application layer needs a role/identity cleanup and explicit services for the missing document workflows.
