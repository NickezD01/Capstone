# Role API Access Matrix

This file answers one frontend question: after a user is authenticated, which role can access which API?

Source files checked:

- `cpms_API/Controllers/*.cs`
- `cpms_API/Program.cs`
- selected service-level ownership checks in `cpms_Application/Services/*.cs`

## Important Reading Notes

- `Public` means no `[Authorize]` attribute is required by the controller/action.
- `Authenticated` means any logged-in, email-verified user with a valid JWT can call the API, regardless of role.
- `ADMIN`, `PM`, and `WAREHOUSE_MANAGER` are the main app roles used by controller authorization.
- The domain also has `SUPPLIER`, `CUSTOMER`, and `WORKER`, but almost no protected business APIs explicitly allow these roles. They can only call APIs marked `Authenticated`, plus public APIs.
- Some APIs have extra service-level ownership checks. Example: a `PM` may be allowed by route attribute, but the service may still require that the PM owns the project.
- Role names must match the JWT role claim: `ADMIN`, `PM`, `WAREHOUSE_MANAGER`, `SUPPLIER`, `CUSTOMER`, `WORKER`.

## Frontend Role Guard Summary

Use this as the high-level menu/sidebar rule:

| Area | ADMIN | PM | WAREHOUSE_MANAGER | CUSTOMER / SUPPLIER / WORKER |
| --- | --- | --- | --- | --- |
| Auth self-service | yes | yes | yes | yes |
| Own profile | yes | yes | yes | yes |
| User/account administration | yes | no | no | no |
| Categories read | yes | yes | yes | yes, even public |
| Categories write | yes | no | no | no |
| Materials read | yes | yes | yes | yes, if logged in |
| Materials write | yes | no | no | no |
| Projects read | yes | yes | yes | no |
| Projects create/import/update owned | no by route for create/update, yes for status actions | yes | no | no |
| Project budget adjustment / manager reassignment | yes | no | no | no |
| Tasks read | yes | yes | yes | no |
| Tasks create/update/lifecycle | no | yes | no | no |
| Progress reports review | no | yes | no | no |
| Material request creation/update/cancel | no | yes | no | no |
| Material request approve/reject/issue/release | no | no | yes | no |
| Purchase order create/procurement/receive/ship | no | no | yes | no |
| Purchase order approve/reject | yes | yes | no | no |
| Suppliers read | yes | yes | yes | no |
| Suppliers write | yes | no | no | no |
| Supplier recommendations | public endpoint | public endpoint | public endpoint | public endpoint |
| Catalog/offers read | yes | yes | yes | no |
| Catalog/offers write | yes | no | no | no |
| Warehouses read | yes | no | yes | no |
| Warehouses create/update | yes | no | no | no |
| Inventory adjustments/count review | yes | no | no | no |
| Inventory operations/count creation/returns | no | no | yes | no |
| Warehouse transfers | read/review yes | no | create/read/ship/receive/cancel yes | no |
| Chat | yes, if participant | yes, if participant | yes, if participant | yes, if participant |
| AI Chat | yes | yes | yes | yes |
| Meetings | yes | yes | yes | yes, if logged in |

## Public APIs

These APIs do not require a JWT.

| Method | API | Purpose |
| --- | --- | --- |
| POST | `/api/Auth/register` | Register a new account. New accounts are created as `CUSTOMER` by default. |
| POST | `/api/Auth/login` | Login and receive access/refresh tokens. Requires verified email. |
| POST | `/api/Auth/Verification` | Verify email with code. |
| POST | `/api/Auth/resend-verification` | Request another verification code. |
| POST | `/api/Auth/refresh` | Exchange refresh token for a new token pair. |
| POST | `/api/Auth/logout` | Revoke refresh token. |
| POST | `/api/Auth/forgot-password` | Request reset code. |
| POST | `/api/Auth/reset-password` | Reset password with reset code. |
| GET | `/api/Categories` | List material categories. |
| GET | `/api/Categories/{id}` | Get category by id. |
| POST | `/api/Suppliers/recommendations/balanced` | Supplier recommendations. No `[Authorize]` is present on this action. |

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
| POST | `/api/Chat/conversations` | Any logged-in user can create a conversation, but service includes current user as participant. |
| GET | `/api/Chat/projects/{projectId}/conversations` | Service returns only conversations where current user is participant. |
| GET | `/api/Chat/conversations/{conversationId}/messages` | Must be participant. |
| POST | `/api/Chat/conversations/{conversationId}/messages` | Must be participant. |
| PUT | `/api/Chat/messages/{messageId}` | Only sender can edit. |
| DELETE | `/api/Chat/messages/{messageId}` | Only sender can delete. |
| PUT | `/api/Chat/conversations/{conversationId}/read` | Must be participant. |
| POST | `/api/AiChat/sessions` | Create AI chat session. Session is scoped to current user. |
| GET | `/api/AiChat/sessions` | Get user's AI chat sessions. |
| GET | `/api/AiChat/sessions/{sessionId}/messages` | Get messages in session owned by user. |
| POST | `/api/AiChat/sessions/{sessionId}/messages` | Send message to AI. User must own session. |
| DELETE | `/api/AiChat/sessions/{sessionId}` | Delete session. User must own session. |
| POST | `/api/Meetings` | Any logged-in user. |
| GET | `/api/Meetings/project/{projectId}` | Any logged-in user by route. |
| GET | `/api/Meetings/{meetingId}` | Any logged-in user by route. |
| PUT | `/api/Meetings/{meetingId}/cancel` | Any logged-in user by route. |

## ADMIN APIs

Only `ADMIN` can call these.

| Method | API | Purpose |
| --- | --- | --- |
| POST | `/api/Auth/admin/reset-password/{userId}` | Queue password reset instructions for a user. |
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
| POST | `/api/Projects/adjust-budget` | Adjust project budget. |
| PUT | `/api/Projects/{projectId}/project-manager` | Reassign project manager. |
| POST | `/api/Warehouses` | Create warehouse. |
| PUT | `/api/Warehouses/{warehouseId}` | Update warehouse. |
| POST | `/api/Warehouses/inventory/adjustments/{adjustmentId}/approve` | Approve inventory adjustment. |
| POST | `/api/Warehouses/inventory/adjustments/{adjustmentId}/reject` | Reject inventory adjustment. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/approve` | Approve physical count. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/reject` | Reject physical count. |

## PM APIs

Only `PM` can call these by controller attribute.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| POST | `/api/Projects` | Create project. Service requires `PMUserID` to equal the current PM's user id. |
| POST | `/api/Projects/import-word` | Import project from Word. Service requires PM role/current user. |
| POST | `/api/Projects/tasks/{taskId}/materials` | Assign planned material requirement to task. Service requires PM manages the project. |
| PUT | `/api/Projects/{projectId}` | Update project. Service requires owning PM and `rowVersion`. |
| POST | `/api/Task` | Create task. |
| GET | `/api/Task/assigned` | Get tasks assigned to current PM by service logic. |
| PUT | `/api/Task/{taskId}` | Update task. |
| POST | `/api/Task/{taskId}/cancel` | Cancel task. |
| POST | `/api/Task/{taskId}/reject` | Reject task. |
| POST | `/api/Task/{taskId}/reopen` | Reopen task. |
| POST | `/api/ProgressReport` | Submit progress report. |
| POST | `/api/ProgressReport/{reportId}/approve` | Approve progress report. |
| POST | `/api/ProgressReport/{reportId}/reject` | Reject progress report. |
| POST | `/api/ProgressReport/{reportId}/correct` | Correct progress report. |
| POST | `/api/ProgressReport/{reportId}/reverse` | Reverse progress report. |
| POST | `/api/MaterialRequest` | Create material request. |
| POST | `/api/MaterialRequest/task/{taskId}` | Create material request from task plan. |
| PUT | `/api/MaterialRequest/{requestId}` | Update pending material request. |
| PUT | `/api/MaterialRequest/{requestId}/cancel` | Cancel pending material request. |

## WAREHOUSE_MANAGER APIs

Only `WAREHOUSE_MANAGER` can call these by controller attribute.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| PUT | `/api/MaterialRequest/{requestId}/approve` | Approve/reserve material request. |
| PUT | `/api/MaterialRequest/{requestId}/reject` | Reject material request. |
| PUT | `/api/MaterialRequest/{requestId}/issue` | Issue reserved material. |
| PUT | `/api/MaterialRequest/{requestId}/release` | Release reservation. |
| POST | `/api/PurchaseOrders` | Create PO. Service requires warehouse managed by current user. |
| GET | `/api/PurchaseOrders/shortages` | View procurement shortages. |
| POST | `/api/PurchaseOrders/from-shortages` | Create PO from shortage lines. |
| POST | `/api/PurchaseOrders/{poId}/receive` | Receive PO into managed warehouse. |
| POST | `/api/PurchaseOrders/{poId}/ship` | Mark PO shipped for managed warehouse. |
| POST | `/api/PurchaseOrders/{poId}/processing` | Mark PO supplier-processing for managed warehouse. |
| POST | `/api/Warehouses/inventory/adjust` | Request inventory adjustment for managed warehouse. |
| POST | `/api/Warehouses/inventory/return` | Return inventory to managed warehouse. |
| POST | `/api/Warehouses/physical-counts` | Start physical count for managed warehouse. |
| POST | `/api/Warehouses/physical-counts/{sessionId}/submit` | Submit physical count for managed warehouse. |
| POST | `/api/WarehouseTransfers` | Create transfer. Must manage source warehouse. |
| POST | `/api/WarehouseTransfers/{id}/ship` | Ship transfer. Must manage source warehouse. |
| POST | `/api/WarehouseTransfers/{id}/receive` | Receive transfer. Must manage destination warehouse. |
| PUT | `/api/WarehouseTransfers/{id}/cancel` | Cancel transfer. Must manage source warehouse. |

## ADMIN + PM APIs

Both `ADMIN` and `PM` can call these.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/ProgressReport/task/{taskId}` | View progress reports for a task. |
| GET | `/api/Projects/{projectId}/budget-histories` | View budget history. Service requires ADMIN or owning PM. |
| POST | `/api/Projects/{projectId}/start` | Start project. Service requires ADMIN or owning PM. |
| POST | `/api/Projects/{projectId}/pause` | Pause project. Service requires ADMIN or owning PM. |
| POST | `/api/Projects/{projectId}/cancel` | Cancel project. Service requires ADMIN or owning PM. |
| POST | `/api/Projects/{projectId}/reopen` | Reopen project. Service requires ADMIN or owning PM. |
| POST | `/api/Projects/{projectId}/complete` | Complete project. Service requires ADMIN or owning PM. |
| PUT | `/api/PurchaseOrders/{id}/approve` | Approve PO. Service allows ADMIN, or PM for their managed project. |
| PUT | `/api/PurchaseOrders/{id}/reject` | Reject PO. Service allows ADMIN, or PM for their managed project. |

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
| GET | `/api/WarehouseTransfers` | Admin all; warehouse manager only transfers involving warehouses they manage. |
| GET | `/api/WarehouseTransfers/{id}` | Admin or manager of source/destination warehouse. |
| PUT | `/api/WarehouseTransfers/{id}/approve` | Admin or destination warehouse manager. Creator cannot approve own transfer unless admin. |
| PUT | `/api/WarehouseTransfers/{id}/reject` | Admin or destination warehouse manager. Creator cannot reject own transfer unless admin. |

## ADMIN + PM + WAREHOUSE_MANAGER APIs

These are the main shared business read APIs.

| Method | API | Purpose / Extra Rule |
| --- | --- | --- |
| GET | `/api/Catalogs` | List catalog offers. |
| GET | `/api/Catalogs/{catalogId}` | Get catalog offer. |
| GET | `/api/MaterialRequest` | List material requests. |
| GET | `/api/MaterialRequest/{requestId}` | Material request detail. |
| GET | `/api/MaterialRequest/project/{projectId}` | Material requests by project. |
| GET | `/api/Projects` | List projects. Service may filter/limit by role/ownership. |
| GET | `/api/Projects/{id}` | Project detail. PM requires access to their project; service checks ownership. |
| GET | `/api/Projects/{projectId}/material-requirements` | Project material requirements. |
| POST | `/api/Projects/{projectId}/mrp-runs` | Calculate MRP. PM must own project; warehouse manager must select a warehouse they manage. |
| GET | `/api/Projects/{projectId}/mrp-runs/latest` | Latest MRP run. PM must own project; warehouse manager must manage warehouse. |
| GET | `/api/PurchaseOrders` | List purchase orders. |
| GET | `/api/PurchaseOrders/{id}` | Purchase order detail. Service checks access to PO. |
| POST | `/api/PurchaseOrders/{poId}/cancel` | Cancel PO. Service restricts by role/status: warehouse manager can cancel own managed pending PO; ADMIN/owning PM can cancel approvable POs. |
| GET | `/api/Suppliers` | List suppliers. |
| GET | `/api/Suppliers/{supplierId}` | Supplier detail. |
| GET | `/api/Task/project/{projectId}` | Tasks by project. |
| GET | `/api/Task/{taskId}` | Task detail. |
| GET | `/api/Task/project/{projectId}/material-requirements` | Task/project material requirements. |

## APIs Not Intended for CUSTOMER, SUPPLIER, WORKER

Even though these roles exist in the domain enum, the controller attributes do not grant them access to the main project/procurement/warehouse/user-admin APIs.

They cannot access APIs restricted to:

- `ADMIN`
- `PM`
- `WAREHOUSE_MANAGER`
- `ADMIN,PM`
- `ADMIN,WAREHOUSE_MANAGER`
- `ADMIN,PM,WAREHOUSE_MANAGER`

They can access:

- Public auth/category/recommendation endpoints.
- Own profile endpoints.
- Authenticated material read endpoints.
- Authenticated chat and meeting endpoints, subject to service-level checks such as conversation participation.

## User List API Example

The user list/count/role APIs are admin-only:

| Method | API | Allowed role |
| --- | --- | --- |
| GET | `/api/UserAccount/GetAllAccountAsync` | `ADMIN` only |
| GET | `/api/UserAccount/CountUser` | `ADMIN` only |
| PUT | `/api/UserAccount/UpdateUserRoleProfile/{customerId}` | `ADMIN` only |
| POST | `/api/Auth/admin/reset-password/{userId}` | `ADMIN` only |

So for frontend:

- Show user list pages only to `ADMIN`.
- Show user-count dashboards only to `ADMIN`.
- Hide role-management actions from `PM`, `WAREHOUSE_MANAGER`, `CUSTOMER`, `SUPPLIER`, and `WORKER`.

