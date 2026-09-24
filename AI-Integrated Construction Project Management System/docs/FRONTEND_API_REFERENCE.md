# BuildSense Backend API Reference for Frontend Chats

> Restructuring update: Phase APIs are implemented. See `FRONTEND_RESTRUCTURING_PLAN.md` and `PHASE_API_IMPLEMENTATION_PLAN.md` for the migration context. Existing task endpoints remain compatible and still use `phaseName` until the task migration is completed.

This document is the frontend-facing map of the backend API discovered from:

- `cpms_API/Program.cs`
- `cpms_API/Controllers/*.cs`
- `cpms_Application/Request/**/*.cs`
- `cpms_Application/Response/**/*.cs`
- `cpms_Application/Validators/WorkflowValidators.cs`

There is also a duplicated project tree at `AI-Integrated Construction Project Management System/AI-Integrated Construction Project Management System`; at the time of this pass, a controller spot-check matched the top-level backend copy. Use the top-level `cpms_API`, `cpms_Application`, `cpms_Domain`, and `cpms_Infrastructure` folders as the working source unless you intentionally sync both copies.

## Runtime Basics

- Backend: ASP.NET Core / .NET 8.
- Local HTTP URL: `http://localhost:5290`.
- Local HTTPS URL: `https://localhost:7143`.
- Swagger UI: `/`.
- OpenAPI JSON: `/swagger/v1/swagger.json`.
- Health checks:
  - `GET /health/live`
  - `GET /health/ready`
- CORS in Development allows any origin, method, and header.
- Auth is JWT Bearer. Send `Authorization: Bearer <accessToken>` for protected endpoints.
- All `/api/Auth/*` calls are rate limited to 5 requests per minute per IP/path. A limit hit returns HTTP 429 with `Retry-After: 60`.
- JWT validation rejects tokens if the account is not found, role changed, or password changed after the token was issued.

## JSON Conventions

ASP.NET Core web defaults normally expose JSON property names in camelCase even though C# DTOs use PascalCase. Examples: `ProjectId` becomes `projectId`, `RowVersion` becomes `rowVersion`.

Most endpoints return:

```json
{
  "statusCode": 200,
  "isSuccess": true,
  "errorMessage": null,
  "result": {}
}
```

Notes:

- `statusCode` is backed by `HttpStatusCode`; it is normally numeric in JSON.
- Validation errors return HTTP 400 with `result` as a field-to-errors map.
- Many mutation endpoints require `rowVersion`. Preserve the latest `rowVersion` returned by the API and send it back on updates/status changes. Conflicts return HTTP 409.
- Enum-typed request body fields are C# enums. Without a `JsonStringEnumConverter` in `Program.cs`, send numeric enum values unless Swagger confirms strings. Status response fields that are declared as `string` return names like `"PENDING"`.

## Roles

Backend roles:

- `ADMIN`
- `PM`
- `WAREHOUSE_MANAGER`
- `SUPPLIER`
- `CUSTOMER`
- `WORKER`

Frontend-relevant roles in route authorization are mostly `ADMIN`, `PM`, and `WAREHOUSE_MANAGER`.

`SUPPLIER` is a retired role: existing supplier accounts are locked out, and `ADMIN` can no longer assign it. Supplier and catalog records remain as admin-managed reference data only.

`WORKER` is the restricted site-worker role: assigned tasks only (`GET /api/Tasks/assigned`, `GET /api/Tasks/{taskId}` for own tasks), a minimal project header (`GET /api/Projects/{id}/context`: name, address, dates), and progress submit/read for own tasks. Everything else returns 403. Task assignment targets the owning PM or a `WORKER`.

`ADMIN` is read-only for project/phase/task/inventory business actions in the target model: it can view data and manage accounts, users, categories, materials, suppliers, catalogs, and project-manager reassignment, but project lifecycle, budget adjustment, and inventory reviews belong to the owning `PM` or the `WAREHOUSE_MANAGER`.

## Endpoint Index

### Auth

Base path: `/api/Auth`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/register` | none (retired) | `UserRegisterRequest` | **HTTP 410 Gone** — accounts are created by an administrator |
| POST | `/login` | Public | `LoginRequest` | `AuthTokenResponse` |
| POST | `/Verification` | none (retired) | `VerificationEmailRequest` | **HTTP 410 Gone** |
| POST | `/resend-verification` | none (retired) | `ResendVerificationRequest` | **HTTP 410 Gone** |
| POST | `/refresh` | Public | `RefreshSessionRequest` | `AuthTokenResponse` |
| POST | `/logout` | Public | `LogoutRequest` | Message/null |
| POST | `/forgot-password` | Public | `ForgotPasswordRequest` | Message |
| POST | `/reset-password` | Public | `ResetPasswordRequest` | Message |
| POST | `/change-password` | Any authenticated user | `ChangePasswordRequest` | Message |
| POST | `/admin/reset-password/{userId}` | `ADMIN` | none | Message |

### User Accounts

Base path: `/api/UserAccount`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| GET | `/GetUserProfile` | Authenticated | none | `UserProfileResponse` |
| PUT | `/UpdateUserProfile` | Authenticated | `UpdateUserRequest` | Message |
| GET | `/GetAllAccountAsync` | `ADMIN` | none | `AccountResponse[]` |
| POST | `/` | `ADMIN` | `CreateUserAccountRequest` (`firstName`, `lastName`, `email`, `phoneNumber?`, `role` except `SUPPLIER`, `password` + `confirmPassword` meeting policy) | HTTP 201, created user id; account is verified immediately |
| GET | `/Customers` | `ADMIN,PM` | query `search?` | `CustomerListResponse[]` (`id`, `firstName`, `lastName`, `email`) |
| GET | `/Workers` | `ADMIN,PM` | query `search?` | `WorkerListResponse[]` (`id`, `firstName`, `lastName`, `email` — for the task-assignment picker) |
| GET | `/GetUserId` | Authenticated | none | `{ userId }` |
| PUT | `/UpdateUserRoleProfile/{customerId}` | `ADMIN` | `UpdateUserRoleRequest` | Message |
| GET | `/CountUser` | `ADMIN` | none | number |

### Projects

Base path: `/api/Projects`

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM` | `CreateProjectRequest` (optional `customerUserId`) | `ProjectResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | none | `ProjectResponse[]` |
| GET | `/{id}` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | none | `ProjectResponse` |
| GET | `/{id}/context` | `WORKER` | none | `ProjectContextResponse` (`projectId`, `projectName`, `address?`, `startDate`, `baselineStart`, `baselineEnd`) — assigned tasks only |
| POST | `/import-word` | `PM` | multipart form-data field `file`, `.docx`, max 10 MB | imported `ProjectResponse` |
| POST | `/import-word-ai` | `PM` | multipart form-data field `file`, `.docx`, max 10 MB | `AiImportPreviewResponse` (`project` draft + `plan` preview; persists nothing) |
| POST | `/tasks/{taskId}/materials` | `PM` | `CreateTaskMaterialRequirementRequest` | task material requirement response/object |
| GET | `/{projectId}/material-requirements` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskMaterialResponse[]` |
| POST | `/{projectId}/mrp-runs` | `PM,WAREHOUSE_MANAGER` | none (`warehouseId` query is accepted but ignored) | `MRPCalculationResponse[]` |
| GET | `/{projectId}/mrp-runs/latest` | `PM,WAREHOUSE_MANAGER` | none (`warehouseId` query is accepted but ignored) | `{ planningRunId, planningVersion, projectId, warehouseId, items }` |
| POST | `/adjust-budget` | `PM` (owning PM only) | `AdjustBudgetRequest` | `ProjectResponse` or budget history object |
| GET | `/{projectId}/budget-histories` | `ADMIN,PM` | none | `ProjectBudgetHistoryResponse[]` |
| GET | `/{projectId}/export` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | none | Excel file download (role-specific sheets, see Project Export) |
| GET | `/{projectId}/risks` | `ADMIN,PM` (owning PM only) | none | `ProjectRiskResponse` (`generatedAt`, `risks[]` with `riskType`, `severity`, `taskId?`, `variantId?`, `message`, `metrics`) |
| POST | `/{projectId}/risks/recommend-actions` | `PM` (owning PM only) | none | `RecommendRiskActionsResponse` (`actions[]` with `priority` 1–5, `title`, `detail`, `ownerRole` PM/WAREHOUSE_MANAGER, `relatedRiskTypes[]`); empty when no risks |
| PUT | `/{projectId}` | `PM` | `UpdateProjectRequest` | updated project/status object |
| POST | `/{projectId}/start` | `PM` (owning PM only) | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/pause` | `PM` (owning PM only) | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/cancel` | `PM` (owning PM only) | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/reopen` | `PM` (owning PM only) | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/complete` | `PM` (owning PM only) | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| PUT | `/{projectId}/project-manager` | `ADMIN` | `ReassignProjectManagerRequest` | updated project/status object |
| PUT | `/{projectId}/customer` | `PM` | `AssignCustomerRequest` (`customerUserId?`, `rowVersion`) | updated `ProjectResponse` |

Project access rules:

- PMs can create only projects assigned to themselves.
- PMs can read/update/change projects they own. Project lifecycle (`start`/`pause`/`cancel`/`reopen`/`complete`) and budget adjustment are owning-PM-only; `ADMIN` can no longer perform them.
- `ADMIN` is read-only for project/phase/task/inventory business actions. Project manager reassignment (`PUT /{projectId}/project-manager`) remains `ADMIN`-only.
- Warehouse managers can read project/MRP data only in allowed contexts; MRP always runs against the single active warehouse, which they must manage.
- CUSTOMER accounts can read only projects explicitly assigned to them (`customerUserId`). Assigning or clearing the customer is done by the owning PM via `PUT /{projectId}/customer`; the target account must hold the `CUSTOMER` role. Reassignment revokes the former customer's access immediately. A closed (`COMPLETED`/`CANCELLED`) project cannot change its customer.
- Closed projects cannot accept many downstream changes.

### Tasks

Canonical base path: `/api/Tasks`. The legacy `/api/task` aliases were removed; `POST /api/task` still returns `410 Gone` pointing to `POST /api/Phases/{phaseId}/tasks`.

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/api/Phases/{phaseId}/tasks` | `PM` | `CreateTaskRequest` (no `ProjectId`/`PhaseName`; project is derived from the phase) | `TaskResponse` |
| POST | `/api/task` | `PM` | none | `410 Gone` - deprecated. Use `POST /api/Phases/{phaseId}/tasks`. |
| GET | `/api/Projects/{projectId}/tasks` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | none | `TaskResponse[]` (assigned customers see their projects) |
| GET | `/api/Tasks/{taskId}` | `ADMIN,PM,WAREHOUSE_MANAGER,WORKER` | none | `TaskResponse` (assigned workers see their own tasks) |
| GET | `/api/Projects/{projectId}/material-requirements` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskMaterialResponse[]` |
| GET | `/api/Tasks/assigned` | `PM,WORKER` | none | `TaskResponse[]` |
| PUT | `/api/Tasks/{taskId}` | `PM` | `UpdateTaskRequest` (has `PhaseId`; the target phase must belong to the same project) | updated task/status object |
| POST | `/api/Tasks/{taskId}/cancel` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |
| POST | `/api/Tasks/{taskId}/reject` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |
| POST | `/api/Tasks/{taskId}/reopen` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |
| POST | `/api/Tasks/{taskId}/issues` | `PM,WORKER` | `CreateTaskIssueRequest` (`description`, `photoUrl?`) | `TaskIssueResponse` (HTTP 201; owning PM or assigned worker) |
| GET | `/api/Tasks/{taskId}/issues` | `ADMIN,PM,WORKER` | none | `TaskIssueResponse[]` (owning PM, assigned worker, or ADMIN) |
| PUT | `/api/Tasks/issues/{issueId}/resolve` | `PM` | `ResolveTaskIssueRequest` (`resolutionNote?`, `rowVersion`) | `TaskIssueResponse` (owning PM only) |

`CreateTaskRequest` body: `taskName`, `assignedToUserID`, `plannedBudget`, `baselineStart`, `baselineEnd`, `materials[]. Task dates must stay inside both the project and the phase baseline. Creating or updating a task under a `COMPLETED`/`CANCELLED` phase returns 409. `assignedToUserID` must be the owning PM (0 or self defaults to self) or a `WORKER`; anything else returns 400.

### Progress Reports

Base path: `/api/ProgressReport`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM,WORKER` | `SubmitProgressReportRequest` | `ProgressReportResponse` (assigned workers submit for their own tasks; PM approval still required) |
| GET | `/task/{taskId}` | `ADMIN,PM,WAREHOUSE_MANAGER,WORKER` | none | `ProgressReportResponse[]` (warehouse manager requires operational project access; assigned workers see their own tasks) |
| POST | `/{reportId}/approve` | `PM` | `ReviewProgressReportRequest` | `ProgressReportResponse` or status object |
| POST | `/{reportId}/reject` | `PM` | `ReviewProgressReportRequest` | `ProgressReportResponse` or status object |
| POST | `/{reportId}/correct` | `PM` | `CorrectProgressReportRequest` | `ProgressReportResponse` |
| POST | `/{reportId}/reverse` | `PM` | `ReviewProgressReportRequest` | `ProgressReportResponse` or status object |

### Categories

Base path: `/api/Categories`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateCategoryRequest` | Message |
| GET | `/` | Public | none | `CategoryResponse[]` |
| GET | `/{id}` | Public | none | `CategoryResponse` |
| PUT | `/{id}` | `ADMIN` | `UpdateCategoryRequest` | Message |
| DELETE | `/{id}` | `ADMIN` | none | Message |

Important route note: file name is `CategoryController.cs`, but class name is `CategoriesController`, so the route is `/api/Categories`.

### Materials

Base path: `/api/Materials`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `MaterialRequest` | `MaterialResponse` or message |
| GET | `/` | Authenticated | none | `MaterialResponse[]` |
| GET | `/{id}` | Authenticated | none | `MaterialResponse` |
| PUT | `/{id}` | `ADMIN` | `UpdateMaterialRequest` | `MaterialResponse` or message |
| DELETE | `/{id}` | `ADMIN` | none | Message |
| POST | `/variants` | `ADMIN` | `MaterialVariantRequest` | `MaterialVariantResponse` |
| GET | `/{materialId}/variants` | Authenticated | none | `MaterialVariantResponse[]` |
| GET | `/variants/{variantId}` | Authenticated | none | `MaterialVariantResponse` |
| PUT | `/variants/{variantId}` | `ADMIN` | `MaterialVariantRequest` | `MaterialVariantResponse` |
| DELETE | `/variants/{variantId}` | `ADMIN` | none | Message |

### Material Requests

Base path: `/api/MaterialRequest`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM` | `CreateMaterialRequest` (`estimatedCost`) | `MaterialRequestResponse` |
| POST | `/task/{taskId}` | `PM` | none | `MaterialRequestResponse` generated from task planned materials |
| PUT | `/{requestId}/approve` | `WAREHOUSE_MANAGER` | `ApproveMaterialRequest` (optional per-item `unitActualCost`) | `MaterialRequestResponse` |
| PUT | `/{requestId}/reject` | `WAREHOUSE_MANAGER` | `RejectMaterialRequest` optional | `MaterialRequestResponse` or message |
| PUT | `/{requestId}` | `PM` | `UpdatePendingMaterialRequest` (optional `estimatedCost`) | `MaterialRequestResponse` |
| PUT | `/{requestId}/cancel` | `PM` | `CancelMaterialRequest` | `MaterialRequestResponse` or message |
| PUT | `/{requestId}/issue` | `WAREHOUSE_MANAGER` | `IssueMaterialRequest` optional (`rowVersion?`, per-item `quantity?` for partial issue) | `MaterialRequestResponse` |
| PUT | `/{requestId}/actual-cost` | `WAREHOUSE_MANAGER` | `AdjustActualCostRequest` (`rowVersion`, per-item `unitActualCost`) | `MaterialRequestResponse` |
| PUT | `/{requestId}/release` | `WAREHOUSE_MANAGER` | none | `MaterialRequestResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `MaterialRequestResponse[]` |
| GET | `/{requestId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `MaterialRequestResponse` |
| GET | `/project/{projectId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `MaterialRequestResponse[]` |

Workflow:

- PM creates a pending request. Inventory is not reserved at creation.
- Warehouse manager approves with warehouse and per-item approved quantities; this reserves inventory.
- Warehouse manager issues active reservations, or releases them.
- PM can update/cancel only pending requests and must send `rowVersion`.
- Requests are capped by task material plans.

Issue-time budget ledger:

- `estimatedCost` (PM, planning only) never debits the budget. `actualCost` (warehouse manager) is the cost basis; `budgetDebitedAmount` tracks what was posted. The client must never set `budgetDebitedAmount`.
- Approving may set per-line `unitActualCost`. At issue, unset lines fall back to the inventory average unit cost and record it on the line.
- Each issue posts an immutable `ISSUE_DEBIT` per line (issued quantity x unit cost), adds to the linked task's `ActualCost`, and accumulates `budgetDebitedAmount`. Re-issuing an already-issued request returns HTTP 409, so debits post exactly once.
- `issue` accepts optional per-line quantities for genuine partial issues; `PartiallyIssued` requests can be re-issued for the remainder, each partial posting its own debit.
- `actual-cost` adjusts per-line unit costs after approval. Only the delta on outstanding (issued but not returned) quantity is posted as `CORRECTION_DELTA`.
- Inventory returns post an explicit `RETURN_REVERSAL` and reduce both `budgetDebitedAmount` and the task actual cost. Releasing a reservation never touches the budget.
- Issuing is rejected with HTTP 409 when ledger spend plus the new debit would exceed `totalProjectBudget` (when set), when the actual cost is negative, or on a stale `rowVersion`.
- Response exposes `estimatedCost`, `actualCost`, `budgetDebitedAmount`, `actualCostUpdatedAt/By`, and per-line `unitActualCost`.

### Purchase Orders

Base path: `/api/PurchaseOrders`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `WAREHOUSE_MANAGER` | `CreatePurchaseOrderRequest` | **HTTP 410 Gone** — procurement writes are retired |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `PurchaseOrderResponse[]` (historical reads) |
| GET | `/{id}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `PurchaseOrderResponse` (historical reads) |
| GET | `/shortages` | `WAREHOUSE_MANAGER` | none | `ProcurementShortageResponse[]` |
| PUT | `/{id}/approve` | `ADMIN,PM` | `PurchaseOrderActionRequest` optional | **HTTP 410 Gone** |
| PUT | `/{id}/reject` | `ADMIN,PM` | `PurchaseOrderActionRequest` optional | **HTTP 410 Gone** |
| POST | `/from-shortages` | `WAREHOUSE_MANAGER` | `CreatePurchaseOrderRequest` | **HTTP 410 Gone** |
| POST | `/{poId}/receive` | `WAREHOUSE_MANAGER` | `ReceivePurchaseOrderRequest` | **HTTP 410 Gone** |
| POST | `/{poId}/ship` | `WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | **HTTP 410 Gone** |
| POST | `/{poId}/processing` | `WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | **HTTP 410 Gone** |
| POST | `/{poId}/cancel` | `ADMIN,PM,WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | **HTTP 410 Gone** |

Procurement writes are retired: material costs are managed through material requests and warehouse actual-cost accounting. The list/detail/shortage reads remain for historical reporting.

### Suppliers

Base path: `/api/Suppliers`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateSupplierRequest` | `SupplierResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `SupplierResponse[]` |
| GET | `/{supplierId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `SupplierResponse` |
| PUT | `/{supplierId}` | `ADMIN` | `UpdateSupplierRequest` | `SupplierResponse` or message |
| DELETE | `/{supplierId}` | `ADMIN` | none | Message |
| POST | `/recommendations/balanced` | none (retired) | `BalancedSupplierRecommendationRequest` | **HTTP 410 Gone** — supplier recommendations are retired |

Supplier recommendations are retired; do not build UI for them.

### Supplier Catalogs

Base path: `/api/Catalogs`

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateCatalogRequest` | `CatalogOfferResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | query `supplierId?`, `variantId?`, `availableOnly=true` | `CatalogOfferResponse[]` |
| GET | `/{catalogId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `CatalogOfferResponse` |
| PUT | `/{catalogId}` | `ADMIN` | `UpdateCatalogRequest` | `CatalogOfferResponse` |
| DELETE | `/{catalogId}` | `ADMIN` | none | Message |

### Warehouses and Inventory

Base path: `/api/Warehouses`

The application has exactly one active operational warehouse (`isActive: true`). The schema and historical `warehouseId` values are retained for reporting, but clients must not offer warehouse creation, selection, or transfers, and must not send `warehouseId` in request bodies — the `warehouseId` fields were removed from `InventoryAdjustmentRequest`, `InventoryReturnRequest`, `StartPhysicalCountRequest`, `CreateMaterialRequest`, and `ApproveMaterialRequest`. Inventory writes and material-request approval always resolve the active warehouse server-side.

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateWarehouseRequest` | **HTTP 410 Gone** — warehouse creation is retired |
| PUT | `/{warehouseId}` | `ADMIN` | `UpdateWarehouseRequest` | **HTTP 410 Gone** — warehouse profile updates are retired |
| GET | `/` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseResponse[]` |
| GET | `/{id}` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseResponse` |
| GET | `/{id}/inventory` | `ADMIN,WAREHOUSE_MANAGER` | none | `InventoryReportResponse[]` |
| GET | `/{warehouseId}/inventory/{variantId}` | `ADMIN,WAREHOUSE_MANAGER` | none | `InventoryReportResponse` |
| POST | `/inventory/adjust` | `WAREHOUSE_MANAGER` | `InventoryAdjustmentRequest` | adjustment summary object |
| GET | `/inventory/adjustments` | `ADMIN,WAREHOUSE_MANAGER` | query `status?` | adjustment summary objects |
| POST | `/inventory/adjustments/{adjustmentId}/approve` | `WAREHOUSE_MANAGER` | `ReviewInventoryAdjustmentRequest` | Message |
| POST | `/inventory/adjustments/{adjustmentId}/reject` | `WAREHOUSE_MANAGER` | `ReviewInventoryAdjustmentRequest` | Message |
| POST | `/inventory/return` | `WAREHOUSE_MANAGER` | `InventoryReturnRequest` | inventory return summary object |
| GET | `/inventory/transactions` | `ADMIN,WAREHOUSE_MANAGER` | query `warehouseId?`, `variantId?` | `InventoryTransactionResponse[]` |
| POST | `/physical-counts` | `WAREHOUSE_MANAGER` | `StartPhysicalCountRequest` | physical count summary object |
| POST | `/physical-counts/{sessionId}/submit` | `WAREHOUSE_MANAGER` | `SubmitPhysicalCountRequest` | `{ sessionId, status, rowVersion }` |
| POST | `/physical-counts/{sessionId}/approve` | `WAREHOUSE_MANAGER` | `ReviewPhysicalCountRequest` | Message |
| POST | `/physical-counts/{sessionId}/reject` | `WAREHOUSE_MANAGER` | `ReviewPhysicalCountRequest` | Message |
| GET | `/physical-counts` | `ADMIN,WAREHOUSE_MANAGER` | query `warehouseId?`, `status?` | physical count summary objects |

Inventory notes:

- Negative inventory adjustments cannot reduce stock below reserved plus quarantined quantities.
- Inventory adjustments and physical counts are reviewed by the `WAREHOUSE_MANAGER` who manages the warehouse. Self-review is allowed, so a manager may approve the adjustment/count they created. A manager who does not manage the warehouse, or an `ADMIN`, receives HTTP 403.
- Open physical count is limited to one per warehouse.
- Returns must link to an issued or partially issued material request.

### Warehouse Transfers

Base path: `/api/WarehouseTransfers`

Warehouse-transfer writes are retired because the application uses a single active warehouse; they return **HTTP 410 Gone**. Only the read endpoints remain for historical records.

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `WAREHOUSE_MANAGER` | `CreateWarehouseTransferRequest` | **HTTP 410 Gone** |
| GET | `/` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse[]` |
| GET | `/{id}` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |
| PUT | `/{id}/approve` | `ADMIN,WAREHOUSE_MANAGER` | none | **HTTP 410 Gone** |
| PUT | `/{id}/reject` | `ADMIN,WAREHOUSE_MANAGER` | none | **HTTP 410 Gone** |
| POST | `/{id}/ship` | `WAREHOUSE_MANAGER` | none | **HTTP 410 Gone** |
| POST | `/{id}/receive` | `WAREHOUSE_MANAGER` | `ReceiveWarehouseTransferRequest` optional | **HTTP 410 Gone** |
| PUT | `/{id}/cancel` | `WAREHOUSE_MANAGER` | none | **HTTP 410 Gone** |

### Chat

Base path: `/api/Chat`

Chat is retired — every endpoint returns **HTTP 410 Gone**.

| Method | Path | Body | Result |
| --- | --- | --- | --- |
| POST | `/conversations` | `CreateConversationRequest` | **HTTP 410 Gone** |
| GET | `/projects/{projectId}/conversations` | none | **HTTP 410 Gone** |
| GET | `/conversations/{conversationId}/messages` | none | **HTTP 410 Gone** |
| POST | `/conversations/{conversationId}/messages` | `SendMessageRequest` | **HTTP 410 Gone** |
| PUT | `/messages/{messageId}` | `UpdateMessageRequest` | **HTTP 410 Gone** |
| DELETE | `/messages/{messageId}` | none | **HTTP 410 Gone** |
| PUT | `/conversations/{conversationId}/read` | none | **HTTP 410 Gone** |

### AI Chat

Base path: `/api/AiChat`

AI chat is retired — every endpoint returns **HTTP 410 Gone**.

| Method | Path | Body | Result |
| --- | --- | --- | --- |
| POST | `/sessions` | `CreateAiChatSessionRequest` | **HTTP 410 Gone** |
| GET | `/sessions` | none | **HTTP 410 Gone** |
| GET | `/sessions/{sessionId}/messages` | none | **HTTP 410 Gone** |
| POST | `/sessions/{sessionId}/messages` | `SendAiChatMessageRequest` | **HTTP 410 Gone** |
| DELETE | `/sessions/{sessionId}` | none | **HTTP 410 Gone** |

AI chat is retired; do not build UI for it. Gemini remains in use for AI construction plan generation only.

### AI Construction Planner

Base path: `/api/AiConstructionPlanner`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| GET | `/questions` | any authenticated user | none | `ConstructionPlannerQuestionsResponse` |
| POST | `/generate-json` | `ADMIN,PM,WAREHOUSE_MANAGER` | `GenerateConstructionPlanRequest` (`projectId?`, `answers`) | `ConstructionPlanJsonResponse` |
| POST | `/generate-excel` | `ADMIN,PM,WAREHOUSE_MANAGER` | `GenerateConstructionPlanExcelRequest` (`projectId?`, `plan`, `fileName?`) | Excel file download (`application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`) |

AI Construction Planner rules:

- `generate-json` and `generate-excel` are staff-only; `CUSTOMER`, `SUPPLIER`, and `WORKER` receive HTTP 403.
- When `projectId` is supplied, the caller must have staff project access: `ADMIN`, the owning PM, or a warehouse manager operationally linked to the project. Otherwise the endpoint returns HTTP 403.
- `generate-excel` expects the `plan` object returned by `generate-json`; an invalid or missing plan returns HTTP 400.

### AI Project Planning (Preview/Confirm)

Base path: `/api/Projects/{projectId}/ai`

Only the owning PM may call these endpoints. Generation is a stateless preview — nothing is persisted until confirm.

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/phases:generate` | `PM` (owning PM only) | `GenerateProjectAiPhasesRequest` (`brief` preferred, or legacy `answers`) | `ProjectAiPlanPreviewResponse` with `phases` (temporary IDs, no `tasks`) |
| POST | `/tasks:generate` | `PM` (owning PM only) | `GenerateProjectAiTasksRequest` (`brief` preferred, or legacy `answers`; plus `phaseId` for one existing phase, or `phases` echo from `phases:generate`) | `ProjectAiPlanPreviewResponse` with `phases` + `tasks` |
| POST | `/confirm` | `PM` (owning PM only) | `ConfirmProjectAiPlanRequest` (final `phases` + `tasks`) | HTTP 201 `ConfirmProjectAiPlanResponse` (`tempId` to real `phaseId`/`taskId`) |
| POST | `/complete` | `PM` (owning PM only) | `CompleteProjectAiPlanRequest` (`brief` preferred, or legacy `answers`; `focusNote?`) | `ProjectAiPlanPreviewResponse` with only new phases/tasks plus `warnings` |

AI project planning rules:

- The frontend owns its question form and sends `AiProjectBriefRequest`: `projectType` (required), `floorAreaM2` (> 0), `numberOfFloors` (≥ 1), `startDate` (required), `endDate` (required, on/after start), `budget?` (≥ 0), `specialRequirements?` (≤ 2000 chars). When both `brief` and legacy `answers` are sent, the brief wins. The legacy five-answer input and `GET /questions` still work.
- `tasks:generate` requires exactly one phase source: an existing `phaseId` (must belong to the project), or the `phases` preview echo. Tasks are mapped to proposed phases by the stable `aiKey`, so the PM may rename phases without breaking the mapping.
- `confirm` accepts the PM-edited proposal: rename/re-date/re-budget items freely, drop unwanted items, but keep temporary IDs stable so tasks still resolve to their phases. Every task must reference exactly one of `phaseTempId` (a phase in the same request) or `phaseId` (an existing phase in the project).
- `confirm` validates phase/task names, dates inside the project and phase baselines, closed phases, duplicate names, and the project budget cap, then creates all phases and tasks in a single transaction. Closed projects return HTTP 409.
- `complete` ("AI finish the planning for me") reads the current phases/tasks, combines them with the brief and optional `focusNote`, and previews only the remaining work. Existing phases become reference entries (empty `TempId` — strip them before `confirm` and keep `PhaseId` on their tasks); duplicates and unresolvable tasks are dropped with reasons in `warnings`.
- Only phases and tasks are created. The AI never touches inventory, approvals, customers, or material master data.

### Project Export

`GET /api/Projects/{projectId}/export` (`ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER`) returns an Excel file built from live project data. The workbook varies by caller; exporting a project the caller cannot access returns HTTP 403.

| View | Caller | Sheets |
| --- | --- | --- |
| Full | `ADMIN`, owning `PM`, assigned `CUSTOMER` | Project, Phases, Tasks, Gantt, Material Requests, Request Lines, Budget Ledger, Budget Summary, Progress |
| Inventory | operationally linked `WAREHOUSE_MANAGER` | Project, Material Requests, Request Lines, Inventory, Stock Movements |

Project export rules:

- The customer workbook is identical to the PM workbook: same request costs, budget impact, and progress details.
- Request Lines show per-line estimate/actual/debited figures derived from the immutable budget ledger; Budget Summary shows total budget, ledger spend, and remaining.
- The Gantt sheet (full view only) shows one row per phase and task across weekly columns from baseline start to end (capped at 104 weeks): green = completed, blue = in progress, gray = pending, ⚠ = at risk. Tasks and Gantt rows include the phase work category.
- The inventory view covers only variants tied to the project's requests at the active warehouse (latest 500 stock movements).
- No user/account administration data is included in any view.

### Meetings

Removed — the controller, service, and tables were deleted. All former endpoints now return 404.

## Request DTOs

Auth and users:

- `UserRegisterRequest`: `email`, `password`, `confirmPassword`, `firstName`, `lastName`
- `LoginRequest`: `userEmail`, `password`
- `VerificationEmailRequest`: `userId`, `verificationCode` (retired)
- `ResendVerificationRequest`: `email` (retired)
- `CreateUserAccountRequest`: `firstName`, `lastName`, `email`, `phoneNumber?`, `role` (not `SUPPLIER`), `password`, `confirmPassword`
- `RefreshSessionRequest`: `refreshToken`, `deviceInfo?`
- `LogoutRequest`: `refreshToken`
- `ForgotPasswordRequest`: `email`
- `ResetPasswordRequest`: `userId`, `token`, `newPassword`, `confirmPassword`
- `ChangePasswordRequest`: `currentPassword`, `newPassword`, `confirmPassword`
- `UpdateUserRequest`: `firstName?`, `lastName?`, `phoneNumber?`, `imgUrl?`
- `UpdateUserRoleRequest`: `role` (`SUPPLIER` is rejected; supplier accounts are retired)

Project/task/progress:

- `CreateProjectRequest`: `projectName`, `address?`, `totalProjectBudget`, `startDate`, `pmUserID`, `baselineStart`, `baselineEnd`, `customerUserId?`
- `UpdateProjectRequest`: `projectName`, `address?`, `startDate`, `baselineStart`, `baselineEnd`, `rowVersion`
- `ProjectLifecycleRequest`: `rowVersion`
- `ReassignProjectManagerRequest`: `projectManagerUserId`, `rowVersion`
- `AssignCustomerRequest`: `customerUserId?` (null clears the assignment), `rowVersion`
- `AdjustBudgetRequest`: `projectId`, `amount`, `reason`
- `CreateTaskRequest`: `taskName`, `assignedToUserID`, `plannedBudget`, `baselineStart`, `baselineEnd`, `materials[]` (the phase comes from the route `phaseId`; the project is derived from the phase)
- `TaskMaterialRequest`: `variantId`, `materialId`, `grossQuantityRequired`
- `UpdateTaskRequest`: `phaseId`, `taskName`, `assignedToUserID`, `plannedBudget`, `baselineStart`, `baselineEnd`, `rowVersion`
- `CreateTaskIssueRequest`: `description`, `photoUrl?`
- `ResolveTaskIssueRequest`: `resolutionNote?`, `rowVersion`
- `TaskIssueResponse`: `issueId`, `taskId`, `reportedByUserId`, `reportedByName`, `description`, `photoUrl?`, `status` (`OPEN`/`RESOLVED`), `resolutionNote?`, `createdAt`, `resolvedAt?`, `rowVersion`
- `TaskLifecycleRequest`: `rowVersion`
- `SubmitProgressReportRequest`: `taskId`, `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`
- `ReviewProgressReportRequest`: `reviewNote?`, `allowCostOverrun`, `rowVersion`
- `CorrectProgressReportRequest`: `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`, `rowVersion`

Materials and material requests:

- `CreateCategoryRequest` / `UpdateCategoryRequest`: `categoryName`
- `MaterialRequest`: `materialName`, `defaultUnit`, `description?`, `isActive`, `categoryId`
- `UpdateMaterialRequest`: `materialName`, `defaultUnit`, `description?`, `isActive`
- `MaterialVariantRequest`: `materialId`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `color?`, `specification?`, `packaging?`, `unit`, `isActive`
- `CreateMaterialRequest`: `projectId`, `taskId?`, `requestNote?`, `estimatedCost` (non-negative, planning only), `items[]`
- `MaterialItemRequest`: `variantId`, `materialId`, `quantity`, `neededByDate`, `note?`
- `CreateTaskMaterialRequirementRequest`: `variantId`, `materialId`, `grossQuantityRequired`
- `ApproveMaterialRequest`: `decisionNote?`, `items[]` (`itemId`, `approvedQuantity`, `unitActualCost?`)
- `ApproveMaterialItemRequest`: `itemId`, `approvedQuantity`, `unitActualCost?`
- `RejectMaterialRequest`: `decisionNote?`
- `UpdatePendingMaterialRequest`: `rowVersion`, `requestNote?`, `estimatedCost?`, `items[]`
- `UpdateMaterialRequestItem`: `itemId`, `quantity`, `neededByDate`, `note?`
- `CancelMaterialRequest`: `rowVersion`, `reason?`
- `IssueMaterialRequest` (optional body): `rowVersion?`, `items[]?` (`itemId`, `quantity`) for partial issue; omit `items` to issue everything reserved
- `AdjustActualCostRequest`: `rowVersion`, `note?`, `items[]` (`itemId`, `unitActualCost`)

Procurement:

- `CreateSupplierRequest`: `companyName`, `contactEmail?`, `contactPhone?`, `address?`, `latitude?`, `longitude?`
- `UpdateSupplierRequest`: `companyName`, `contactEmail?`, `contactPhone?`
- `CreateCatalogRequest`: `supplierId`, `variantId`, `materialId`, `supplierSku?`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `UpdateCatalogRequest`: `supplierSku?`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `BalancedSupplierRecommendationRequest`: `projectId?`, `items[]`, `costWeight`, `reliabilityWeight`, `leadTimeWeight`, `maxRecommendations`, `searchWebForNearbySuppliers`, `warehouseLocation?`, `searchRadiusKm`, `regionCode?` (retired)
  - `warehouseLocation`: free-text location used to build the Tavily query, for example `"District 7, Ho Chi Minh City"`.
  - `searchRadiusKm`: search radius hint included in the Tavily query. Defaults to `30` when omitted or `<= 0`.
  - `regionCode?`: optional region hint appended to the Tavily query, for example `"VN"`.
- `RequestedMaterialItem`: `materialId`, `quantity`
- `CreatePurchaseOrderRequest`: `projectId`, `supplierId`, `warehouseId`, `expectedDeliveryDate?`, `note?`, `items[]`
- `OrderLineItemDto`: `variantId`, `materialId`, `requestItemId?`, `quantity`, `unitPrice`
- `PurchaseOrderActionRequest`: `note?`, `rowVersion?`
- `ReceivePurchaseOrderRequest`: `note?`, `rowVersion?`, `isFinalDelivery`, `items[]`
- `ReceivePurchaseOrderItemRequest`: `lineItemId`, `quantity`, `damagedQuantity`, `missingQuantity`, `lotNumber?`, `batchNumber?`, `serialNumber?`, `expiryDate?`

Warehouse/inventory:

- `CreateWarehouseRequest`: `managerId`, `warehouseName`, `location` (endpoint returns HTTP 410)
- `UpdateWarehouseRequest`: `managerId`, `warehouseName`, `location`
- `InventoryAdjustmentRequest`: `variantId`, `quantityDelta`, `reasonCode`, `note?`, `rowVersion?`
- `ReviewInventoryAdjustmentRequest`: `rowVersion`, `reviewNote?`
- `InventoryReturnRequest`: `variantId`, `quantity`, `materialRequestId`, `reasonCode`, `condition`, `note?`, `rowVersion?`
- `StartPhysicalCountRequest`: `variantIds[]`, `note?`
- `SubmitPhysicalCountRequest`: `rowVersion`, `lines[]`
- `PhysicalCountQuantityRequest`: `lineId`, `actualQuantity`
- `ReviewPhysicalCountRequest`: `rowVersion`, `reviewNote?`
- `CreateWarehouseTransferRequest`: `sourceWarehouseId`, `destinationWarehouseId`, `note?`, `items[]`
- `CreateWarehouseTransferItemRequest`: `variantId`, `quantity`
- `ReceiveWarehouseTransferRequest`: `items[]`
- `ReceiveWarehouseTransferItemRequest`: `transferItemId`, `quantity`, `damagedQuantity`, `lostQuantity`

Chat (retired) and meetings:

- `CreateConversationRequest`: `projectId`, `taskId?`, `title`, `type`, `participantUserIds[]` (retired)
- `SendMessageRequest`: `body`, `attachmentUrl?` (retired)
- `UpdateMessageRequest`: `body` (retired)
- `CreateAiChatSessionRequest`: `title?`, `projectId?` (retired)
- `SendAiChatMessageRequest`: `message`, `useWebSearch` (retired)
- `CreateMeetingRequest`: removed with the meetings feature.
- `MeetingParticipantRequest`: removed with the meetings feature.
- `CancelMeetingRequest`: removed with the meetings feature.

## Main Response DTOs

Use these as the shape inside `result`.

- `AuthTokenResponse`: `accessToken`, `refreshToken`, `accessTokenExpiresAt`, `refreshTokenExpiresAt`
- `UserProfileResponse`: `id`, `firstName`, `lastName`, `email`, `phoneNumber`, `imgUrl?`, `role`
- `AccountResponse`: `id`, `firstName`, `lastName`, `email`, `phoneNumber`, `role`
- `CustomerListResponse`: `id`, `firstName`, `lastName`, `email`
- `WorkerListResponse`: `id`, `firstName`, `lastName`, `email`
- `ProjectResponse`: `projectId`, `projectName`, `address?`, `status`, `createdDate`, `startDate`, `baselineStart`, `baselineEnd`, `totalProjectBudget`, `budgetConfigured`, `actualCost`, `plannedTaskBudget`, `reportedTaskActualCost`, `purchaseOrderCommittedCost`, `purchaseOrderReceivedCost`, `remainingProcurementBudget`, `currency`, `pmUserID`, `pmName`, `customerUserId?`, `customerName?`, `totalTasks`, `totalAIAlerts`, `rowVersion`
- `ProjectBudgetHistoryResponse`: `id`, `projectId`, `amountChanged`, `previousBudget`, `newBudget`, `currency`, `reason`, `updatedByUserId`, `createdAt`
- `TaskResponse`: `taskId`, `projectId`, `phaseId`, `phaseName`, `phase` (`phaseId`, `name`, `sequenceOrder`, `status`, `baselineStart`, `baselineEnd`), `taskName`, `assignedToUserID`, `assignedToUserName`, `plannedBudget`, `actualCost`, `actualProgressPct`, `status`, `baselineStart`, `baselineEnd`, `rowVersion`, `materialRequirements[]`
- `TaskMaterialResponse`: `variantId`, `materialId`, `materialName`, `variantName`, `taskName?`, `grossQuantityRequired`, `unit`
- `ProgressReportResponse`: `reportId`, `taskId`, `taskName`, `reportedByUserId`, `reportedByName`, `reportDate`, `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`, `status`, `reviewedByUserId?`, `reviewedAt?`, `reviewNote?`, `originalReportId?`, `rowVersion`
- `CategoryResponse`: `id`, `categoryName`, `totalMaterials`
- `MaterialResponse`: `materialId`, `materialName`, `defaultUnit`, `description?`, `isActive`, `categoryId`, `variants[]`
- `MaterialVariantResponse`: `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `color?`, `specification?`, `packaging?`, `unit`, `isActive`
- `MaterialRequestResponse`: `requestId`, `projectId`, `taskId?`, `warehouseId?`, `warehouseName?`, `requestedBy`, `requestedByName`, `requestDate`, `status`, `requestNote?`, `approvedByUserId?`, `approvedAt?`, `decisionNote?`, `estimatedCost`, `actualCost`, `budgetDebitedAmount`, `actualCostUpdatedAt?`, `actualCostUpdatedByUserId?`, `rowVersion`, `items[]`
- `MaterialRequisitionDetailResponse`: `itemId`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `unit?`, `quantity`, `approvedQuantity`, `issuedQuantity`, `unitActualCost`, `returnedQuantity`, `netIssuedQuantity`, `remainingRequestQuantity`, `remainingTaskDemand`, `neededByDate`, `note?`
- `MRPCalculationResponse`: `variantId`, `warehouseId?`, `inventoryScope`, `materialId`, `materialName`, `variantName`, `unit`, `totalGrossRequired`, `issuedToProjectTasks`, `remainingGrossRequired`, `currentInventory`, `reservedQuantity`, `availableQuantity`, `onOrderQuantity`, `netQuantityRequired`, `earliestStartDate`, `planningRunId`, `planningVersion`, `transferRecommendations[]`
- `SupplierResponse`: `supplierId`, `companyName`, `contactEmail?`, `contactPhone?`
- `CatalogOfferResponse`: `catalogId`, `supplierId`, `supplierName`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `supplierSku?`, `unit`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `BalancedSupplierRecommendationResponse`: `usedGoogleAI`, `usedWebSearch`, `strategy`, `aiSummary?`, `webSearchSummary?`, `recommendations[]` (retired)
  - `recommendations[].source`: `InternalCatalog` or `WebSearch`.
- `PurchaseOrderResponse`: `poId`, `project`, `supplier`, `status`, `currency`, `totalAmount`, `warehouseId`, `warehouseName`, `orderDate`, `expectedDeliveryDate?`, `approvedByUserId?`, `approvedAt?`, `note?`, `rowVersion`, `items[]`
- `OrderLineItemResponse`: `orderLineItemId`, `variantId`, `materialId`, `requestItemId?`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `specification?`, `packaging?`, `unit`, `quantity`, `receivedQuantity`, `damagedQuantity`, `missingQuantity`, `accountedQuantity`, `remainingQuantity`, `unitPrice`, `subTotal`
- `ProcurementShortageResponse`: `projectId`, `projectName`, `taskId?`, `warehouseId`, `warehouseName`, `requestItemId`, `requestIds[]`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `unit`, `neededByDate`, `grossShortageQuantity`, `procurementCoverageQuantity`, `remainingShortageQuantity`, `supplierOffers[]`
- `WarehouseResponse`: `warehouseId`, `warehouseName`, `location`, `managerId`, `managerName?`, `isActive`, `inventoryRecords[]`, `createdDate`, `modifiedDate?`, `createdBy?`, `modifiedBy?`, `isDeleted`
- `InventoryReportResponse`: `inventoryId`, `warehouseId`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `specification?`, `packaging?`, `warehouseName`, `unit`, `quantityOnHand`, `reservedQuantity`, `onOrderQuantity`, `availableQuantity`, `reorderLevel`, `quarantineQuantity`, `averageUnitCost`, `inventoryValue`, `isLowStock`, `updatedAt`, `rowVersion`
- `InventoryTransactionResponse`: `transactionId`, `warehouseId`, `variantId`, `transactionType`, `quantity`, `quantityBefore`, `quantityAfter`, `referenceId?`, `referenceType?`, `note?`, `performedByUserId`, `transactionDate`, `unitCost?`, `totalValue?`, `lotNumber?`, `batchNumber?`, `serialNumber?`, `expiryDate?`
- `WarehouseTransferResponse`: `transferId`, `sourceWarehouseId`, `sourceWarehouseName`, `destinationWarehouseId`, `destinationWarehouseName`, `status`, `requestedByUserId`, `approvedByUserId?`, `shippedByUserId?`, `receivedByUserId?`, `requestedAt`, `approvedAt?`, `shippedAt?`, `receivedAt?`, `note?`, `rowVersion`, `items[]`
- `ConversationResponse`: `conversationId`, `projectId`, `taskId?`, `title`, `type`, `lastMessageAt`, `participants[]` (retired)
- `MessageResponse`: `messageId`, `conversationId`, `senderId`, `senderName?`, `body`, `attachmentUrl?`, `sentAt`, `editedAt?`, `deletedAt?` (retired)
- `AiChatSessionResponse`: `sessionId`, `userId`, `projectId?`, `title`, `createdAt?`, `lastMessageAt?`, `messageCount` (retired)
- `AiChatMessageResponse`: `messageId`, `sessionId`, `role`, `content`, `createdAt`, `sentAt` (retired)
- `AiChatReplyResponse`: `userMessage` (`AiChatMessageResponse`), `assistantMessage` (`AiChatMessageResponse`), `usedWebSearch`, `webSearchSources[]` (`title`, `url`) (retired)
- `MeetingResponse`: removed with the meetings feature.

## Status and Code Values

Project status:

- `PLANNING`
- `IN_PROGRESS`
- `COMPLETED`
- `DELAYED`
- `PAUSED`
- `CANCELLED`

Task status:

- `PENDING`
- `ACTIVE`
- `IN_PROGRESS`
- `COMPLETED`
- `REJECTED`
- `CANCELLED`

Progress report status:

- `PENDING`
- `APPROVED`
- `REJECTED`
- `CORRECTED`
- `REVERSED`

Material request status:

- `PENDING`
- `APPROVED`
- `PARTIALLY_APPROVED`
- `REJECTED`
- `ISSUED`
- `PARTIALLY_ISSUED`
- `RELEASED`
- `CANCELLED`

Purchase order status:

- `PENDING`
- `APPROVED`
- `PROCESSING`
- `SHIPPED`
- `PARTIALLY_RECEIVED`
- `REJECTED`
- `DELIVERED`
- `CLOSED_WITH_VARIANCE`
- `CANCELLED`

Warehouse transfer status (legacy read-only; transfer writes return HTTP 410):

- `REQUESTED`
- `APPROVED`
- `IN_TRANSIT`
- `RECEIVED`
- `CLOSED_WITH_VARIANCE`
- `REJECTED`
- `CANCELLED`

Inventory/warehouse constants:

- Inventory adjustment statuses: `PENDING`, `APPROVED`, `REJECTED`
- Inventory adjustment reasons: `CYCLE_COUNT`, `DAMAGE`, `LOSS`, `DATA_CORRECTION`, `OPENING_BALANCE`
- Inventory transaction types: `RECEIPT`, `ISSUE`, `RETURN`, `ADJUSTMENT`, `TRANSFER_OUT`, `TRANSFER_IN`, `PHYSICAL_COUNT`
- Inventory reservation statuses: `ACTIVE`, `RELEASED`, `FULFILLED`
- Material return reasons: `UNUSED`, `EXCESS_ISSUE`, `DAMAGED`
- Material return conditions: `USABLE`, `QUARANTINED`
- Physical count statuses: `DRAFT`, `PENDING_APPROVAL`, `APPROVED`, `REJECTED`
- Transfer reservation statuses: `ACTIVE`, `CONSUMED`, `RELEASED`

Chat/meeting enum meanings:

- Chat conversation type: `PROJECT`, `TASK`, `MATERIAL_REQUEST`, `PURCHASE_ORDER` (retired)
- AI chat role: `User` / `Assistant` (enum values `0` / `1` if sent as numbers) (retired)
- Meeting provider: `MICROSOFT_TEAMS`
- Meeting status: `DRAFT`, `SCHEDULED`, `FAILED`, `CANCELLED`
- Meeting participant role: `REQUIRED`, `OPTIONAL`

## AI Provider Split (Frontend Notes)

The backend uses two providers with separate responsibilities:

| Provider | Used for | Frontend trigger |
| --- | --- | --- |
| **Tavily** | No live endpoint (web search belonged to retired AI chat and recommendations) | none |
| **Gemini** | AI construction plan generation | AI project planning answers (`phases:generate`, `tasks:generate`, `generate-json`) |

Frontend UX suggestions:

- Surface backend `errorMessage` directly for missing-provider configuration (`Tavily:ApiKey...`, `GoogleAI:ApiKey...`).
- Do not build UI for chat, AI chat, meetings, or supplier recommendations; chat/AI-chat/meeting code and tables were deleted (calls now 404), while PO writes, transfers writes, and recommendations still return HTTP 410.

Backend configuration keys (for DevOps, not sent by frontend):

```json
{
  "GoogleAI": {
    "ApiKey": "<gemini-key>",
    "Model": "gemini-3.5-flash"
  },
  "Tavily": {
    "ApiKey": "<tavily-key>",
    "DefaultMaxResults": 5,
    "SearchDepth": "basic"
  }
}
```

## Phase APIs (Implemented)

The first restructuring slice adds phase APIs while keeping existing task endpoints compatible. `TaskItem.PhaseId` is currently nullable; do not remove or stop sending the legacy `phaseName` field until the task migration is released.

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/api/Projects/{projectId}/phases` | `PM` | `CreatePhaseRequest` | `PhaseResponse` (HTTP 201) |
| GET | `/api/Projects/{projectId}/phases` | `ADMIN,PM,WAREHOUSE_MANAGER,CUSTOMER` | none | `PhaseResponse[]` ordered by `sequenceOrder`, then `name` (assigned customers see their projects) |
| GET | `/api/Phases/{phaseId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `PhaseResponse` |
| PUT | `/api/Phases/{phaseId}` | `PM` | `UpdatePhaseRequest` | updated `PhaseResponse` |
| POST | `/api/Phases/{phaseId}/cancel` | `PM` | `PhaseLifecycleRequest` | `{ phaseId, status, rowVersion }` |

Phase request fields:

- `CreatePhaseRequest`: `name`, `description?`, `sequenceOrder`, `baselineStart`, `baselineEnd`.
- `UpdatePhaseRequest`: the create fields plus `rowVersion`.
- `PhaseLifecycleRequest`: `rowVersion`.

Phase status values are `PLANNED`, `IN_PROGRESS`, `COMPLETED`, and `CANCELLED`. Phase names are unique within a project. Phase dates must remain inside the project baseline. Keep the latest `rowVersion` and send it on updates and cancellation; stale values return HTTP 409.

Project customer assignment is now implemented (see Projects), so an assigned customer can read their project. The phase routes below still authorize only `ADMIN`, `PM`, and `WAREHOUSE_MANAGER`; enabling customer phase reads is a follow-up that will reuse the same project-access check.

## Validation Highlights

- Passwords: 10-128 chars, at least one uppercase, lowercase, and number.
- Reset token: 6 digits. (Email verification is retired; only password reset uses emailed codes.)
- Project: `totalProjectBudget >= 0`, baseline end must be on/after baseline start, start date must be on/before baseline end and service also requires it inside baseline period.
- Task: planned budget cannot be negative; baseline end must be on/after baseline start.
- Progress increments: greater than 0 and at most 100, 2 decimal precision; actual cost increments cannot be negative.
- Catalog available offers must have positive unit price; lead time and minimum order quantity cannot be negative.
- Create material request: `projectId > 0`, at least one item, quantities greater than 0, no duplicate variant per request.
- Approve material request: at least one item, approved quantities are nonnegative and at least one should be positive; approval targets the active warehouse.
- Create purchase order: retired (HTTP 410).
- Receive purchase order: retired (HTTP 410).
- Inventory adjustment: `quantityDelta` cannot be zero; reason must be one of inventory adjustment reasons.
- Warehouse transfer: writes are retired (HTTP 410); the legacy rules (distinct source/destination, no duplicate variants, positive quantities) only apply to historical records.

## Frontend Workflow Cheat Sheet

1. Register/login through `/api/Auth`.
2. Store `accessToken`, `refreshToken`, and expiry dates from `AuthTokenResponse`.
3. Add `Authorization: Bearer <accessToken>` to protected calls.
4. On any edit screen, keep `rowVersion` from the latest read.
5. On HTTP 409, refetch the entity and let the user retry with the latest `rowVersion`.
6. For project planning: create project, create tasks, assign task material requirements, run MRP (always against the single active warehouse).
7. For material fulfillment: PM creates material request with an estimate, warehouse manager approves/reserves (optionally setting unit costs), then issues (posting budget debits) or releases. Returns post reversals; cost corrections post deltas.
8. For procurement: retired. Purchase-order writes return HTTP 410; only list/detail/shortage reads remain for history.
9. For inventory corrections: warehouse manager submits adjustment/count and reviews them (self-review allowed); the reviewer must manage the warehouse.
10. For chat: retired. All chat endpoints return HTTP 410.
11. For AI chat: retired. All AI chat endpoints return HTTP 410.
12. For AI construction planning: fetch questions, call `generate-json` (staff only), then `generate-excel` with the returned plan; include `projectId` for project-scoped access checks.
13. For AI project plan persistence (owning PM only): call `phases:generate`, let the PM edit the preview, optionally call `tasks:generate` with the phase echo, then `confirm` with the final proposal; keep temporary IDs stable so tasks resolve to their phases.
14. For project export: call `GET /api/Projects/{projectId}/export` and download the file; the workbook sheets depend on the caller role (full view for ADMIN/owning PM/assigned customer, inventory view for a linked warehouse manager).
15. For AI supplier recommendations: retired. The endpoint returns HTTP 410.
