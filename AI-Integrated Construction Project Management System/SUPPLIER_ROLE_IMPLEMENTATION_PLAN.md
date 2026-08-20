# Supplier Role Flow Implementation Plan

## Goal

Add a secure supplier portal flow in which an administrator can create or attach a verified supplier login, and the linked supplier can view and act on only that supplier's purchase orders. Warehouse managers continue to create and receive purchase orders, while administrators and PMs continue to approve or reject them.

## Product Rules

- `Role.SUPPLIER` already exists and must be used for supplier portal accounts.
- Public registration must continue to create `CUSTOMER` accounts. It must never create `SUPPLIER` accounts.
- A supplier company may have at most one linked login account.
- A supplier login must be explicitly provisioned by an administrator.
- The linked supplier account is verified during provisioning; the user receives a password-reset email/code rather than a password being returned or shown to an administrator.
- A supplier can see only purchase orders belonging to its linked supplier record.
- A supplier can mark an approved purchase order as `PROCESSING`, then mark it as `SHIPPED`.
- A supplier can decline an `APPROVED` or `PROCESSING` purchase order before shipment. Declining uses the existing cancel behavior and must preserve the existing `OnOrderQuantity` rollback.
- A supplier cannot approve, reject, receive, or create purchase orders.
- Warehouse managers continue to create purchase orders and receive shipments.
- Administrators and PMs continue to approve or reject pending purchase orders.
- A supplier cannot access catalog writes, warehouse operations, inventory operations, or other suppliers' data.

## Target Workflow

```text
ADMIN
  POST /api/Suppliers with createLoginAccount=true
    -> create Supplier + linked UserAccount with role SUPPLIER
    -> mark email verified
    -> queue password-reset email

SUPPLIER
  POST /api/Auth/reset-password
  POST /api/Auth/login
  GET  /api/PurchaseOrders
    -> only linked supplier's purchase orders

ADMIN / PM
  PUT /api/PurchaseOrders/{id}/approve or reject
    -> APPROVED or rejected

SUPPLIER
  POST /api/PurchaseOrders/{id}/processing
    -> APPROVED -> PROCESSING
  POST /api/PurchaseOrders/{id}/shipped
    -> PROCESSING -> SHIPPED
  POST /api/PurchaseOrders/{id}/cancel
    -> APPROVED/PROCESSING -> cancelled

WAREHOUSE_MANAGER
  POST /api/PurchaseOrders/{id}/receive
    -> receive shipment and update inventory
```

## Phase 1: Link Supplier to User Account

### Domain and EF model

- Add nullable `UserAccountId` to `Supplier`.
- Add `UserAccount?` navigation on `Supplier`.
- Add inverse `Supplier? LinkedSupplier` navigation on `UserAccount`.
- Configure a unique filtered index on `Suppliers.UserAccountId` where the value is not null.
- Use `DeleteBehavior.Restrict` so deleting a user cannot silently delete the supplier company.
- Add an EF migration and verify it against existing supplier data.
- Existing suppliers remain valid with `UserAccountId = null` until an administrator provisions or attaches an account.

### Responses and requests

Extend `SupplierResponse` with:

- `userAccountId` nullable
- `loginEmail` nullable
- optionally `loginProvisioned` or derive it from `userAccountId`

Extend `CreateSupplierRequest` with:

- `createLoginAccount` boolean, default `false`
- `loginEmail` nullable, defaulting to `contactEmail` when login creation is requested
- `firstName` nullable
- `lastName` nullable

Validation rules:

- `loginEmail`, `firstName`, and `lastName` are required when `createLoginAccount = true`.
- `loginEmail` must be a valid email and must not already belong to another user.
- A supplier cannot be linked to a second account.
- A user cannot be linked to multiple suppliers.

## Phase 2: Admin Provisioning and Attachment

### Create supplier with login

Update `POST /api/Suppliers` so an administrator can request login provisioning in the same operation.

Provisioning must:

1. Validate the supplier and login data.
2. Reject duplicate email addresses and existing supplier links.
3. Create a `UserAccount` with `Role.SUPPLIER` and `IsEmailVerified = true`.
4. Generate a cryptographically random password hash/salt. Do not return or log the password.
5. Set `Supplier.UserAccountId`.
6. Queue the existing password-reset email with the user id and a short-lived reset code.
7. Commit the supplier, user, link, and outbox message atomically.
8. Return supplier data without password, hash, salt, reset code, or other secret material.

Reuse the existing `IAuthService` password-reset/provisioning pattern, for example a method such as `ProvisionVerifiedUserAsync`, so password hashing and email-outbox behavior are not duplicated in `SupplierService`.

### Attach an account to an existing supplier

Add an admin-only endpoint:

```text
POST /api/Suppliers/{supplierId}/account
```

Request body:

```json
{
  "loginEmail": "supplier@example.com",
  "firstName": "Supplier",
  "lastName": "User"
}
```

The endpoint uses the same provisioning rules as supplier creation. It must reject:

- a missing supplier;
- a supplier already linked to a user;
- an email already assigned to another account;
- an account already linked to a different supplier.

### Supplier lifecycle rules

- When a supplier is deactivated, soft-delete or disable the linked user so the supplier cannot log in.
- When a supplier is reactivated, do not automatically reactivate the login without an explicit administrative action.
- `UpdateUserRoleProfile` must reject changing the role or profile of a user linked to a supplier in a way that breaks the supplier portal contract.
- The generic role-update API must not allow assigning `SUPPLIER`; create/attach endpoints are the only supported path.

## Phase 3: Purchase Order Authorization

### Read access

Update `PurchaseOrderService.GetAllPurchaseOrdersAsync` and the detail authorization path:

```text
SUPPLIER => purchaseOrder.Supplier.UserAccountId == currentUser.Id
```

Apply the same ownership rule to:

- `GET /api/PurchaseOrders`
- `GET /api/PurchaseOrders/{id}`
- any purchase-order summary endpoint

The filter must be applied in the query/service layer, not only in the frontend. A supplier must receive an authorization failure or not-found response for another supplier's order without leaking its details.

### Supplier actions

Update `PurchaseOrderController` and `PurchaseOrderService` with supplier authorization for:

- processing: only linked supplier, only from `APPROVED`;
- shipping: only linked supplier, only from `APPROVED` or `PROCESSING` as allowed by the existing lifecycle;
- cancellation/decline: only linked supplier, only from `APPROVED` or `PROCESSING`, never after `SHIPPED` or `RECEIVED`.

Prefer explicit endpoint names such as:

```text
POST /api/PurchaseOrders/{id}/processing
POST /api/PurchaseOrders/{id}/shipped
POST /api/PurchaseOrders/{id}/cancel
```

If the existing API uses `PUT` or action-specific request bodies, preserve its public convention and document the final method/path in `FRONTEND_API_REFERENCE.md`.

### Non-supplier actions

Preserve these permissions:

| Action | Allowed roles |
| --- | --- |
| Create purchase order | `WAREHOUSE_MANAGER` |
| Approve/reject pending order | `ADMIN`, `PM` |
| Mark processing/shipped | linked `SUPPLIER` only |
| Cancel/decline approved or processing order | linked `SUPPLIER` only |
| Receive shipment | `WAREHOUSE_MANAGER` |

Do not broaden supplier access to catalog writes, warehouse APIs, inventory APIs, or unrelated project APIs.

## Phase 4: Password and Email Flow

- Reuse the existing reset-code generation, hashing, expiry, and email-outbox implementation.
- Password-reset codes must be short-lived, single-use, and stored only in their protected representation.
- Do not include passwords, reset codes, API keys, or tokens in API responses or logs.
- Ensure provisioning failure rolls back the account/link and does not leave an unusable partial supplier login.
- Show the administrator only a non-secret status such as `passwordResetEmailQueued: true`.

## Phase 5: Documentation and Frontend Contract

Update [FRONTEND_API_REFERENCE.md](FRONTEND_API_REFERENCE.md) with:

- supplier creation fields and validation;
- admin attach-account endpoint;
- supplier login/reset flow;
- supplier PO list/detail response scope;
- processing, shipping, and decline action requests/responses;
- error responses for unlinked or unauthorized suppliers.

Update [ROLE_API_ACCESS_MATRIX.md](ROLE_API_ACCESS_MATRIX.md) with:

- supplier account provisioning;
- supplier-scoped PO list/detail;
- supplier processing, shipping, and decline actions;
- explicit prohibition on supplier receiving or approving orders.

Frontend behavior:

- Admin supplier form includes a `Create supplier login` checkbox.
- When selected, collect first name, last name, and login email, prefilled from contact email where appropriate.
- Show a non-secret confirmation that the password-reset email was queued.
- Supplier dashboard loads only `GET /api/PurchaseOrders` and optional `/summary`.
- Supplier action buttons appear only for eligible statuses: `Processing`, `Ship`, and `Decline`.
- Receiving remains in the warehouse-manager UI.

## Phase 6: Tests

Add or update tests in `cpms_Tests` for:

### Account provisioning

- Creating a supplier without login leaves `UserAccountId` null.
- Creating a supplier with login creates a verified `SUPPLIER` account.
- Provisioning queues a password-reset email and never returns a password.
- Existing supplier attachment works.
- Duplicate login email is rejected.
- Supplier already linked to an account is rejected.
- One user cannot be linked to multiple suppliers.
- Public registration still creates `CUSTOMER`, never `SUPPLIER`.
- Deactivating a supplier prevents linked-user login.

### Purchase-order authorization

- Supplier list is scoped to the linked supplier.
- Supplier cannot read another supplier's PO by list or id.
- Linked supplier can mark an approved PO as processing.
- Linked supplier can mark an eligible PO as shipped.
- Linked supplier can decline approved or processing PO.
- Declining rolls back `OnOrderQuantity` according to existing behavior.
- Supplier cannot process an unapproved PO.
- Supplier cannot ship a cancelled, received, or otherwise invalid PO.
- Supplier cannot approve, reject, create, or receive a PO.
- Warehouse manager cannot mark a PO as shipped through the supplier action.
- PM/admin approval behavior remains unchanged.

Update the existing PO lifecycle regression test in [BusinessRuleRegressionTests.cs](cpms_Tests/BusinessRuleRegressionTests.cs) so processing/shipping uses a matching supplier claim and verifies a warehouse-manager claim is rejected.

## Suggested Implementation Order

1. Add the supplier-user relationship and migration.
2. Add request/response models and validators.
3. Extract or reuse verified-user provisioning and password-reset outbox logic.
4. Implement admin create/attach account endpoints.
5. Add supplier ownership filtering for PO reads.
6. Add supplier-only processing, shipping, and decline authorization.
7. Add focused business and authorization tests.
8. Update API reference and role matrix.
9. Run the full test suite and verify the frontend against the updated contract.

## Acceptance Criteria

- A supplier can be provisioned by an administrator without any password being exposed.
- The supplier can reset its password, log in, and receive a `SUPPLIER` JWT role claim.
- The supplier sees only its own purchase orders.
- The supplier can process, ship, or decline only eligible orders belonging to its company.
- Warehouse managers still create and receive orders; admins/PMs still approve orders.
- Cross-supplier reads and actions are denied at the backend service/controller boundary.
- Existing public registration and existing non-supplier workflows remain unchanged.
- Database migration, tests, API reference, and role matrix are all updated.
