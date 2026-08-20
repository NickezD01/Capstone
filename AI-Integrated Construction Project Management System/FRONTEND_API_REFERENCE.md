# BuildSense Backend API Reference for Frontend Chats

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
- JWT validation rejects tokens if the account is not found, email is unverified, role changed, or password changed after the token was issued.

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

## Endpoint Index

### Auth

Base path: `/api/Auth`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/register` | Public | `UserRegisterRequest` | Message; account verification email queued |
| POST | `/login` | Public | `LoginRequest` | `AuthTokenResponse` |
| POST | `/Verification` | Public | `VerificationEmailRequest` | Message |
| POST | `/resend-verification` | Public | `ResendVerificationRequest` | Message |
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
| GET | `/GetUserId` | Authenticated | none | `{ userId }` |
| PUT | `/UpdateUserRoleProfile/{customerId}` | `ADMIN` | `UpdateUserRoleRequest` | Message |
| GET | `/CountUser` | `ADMIN` | none | number |

### Projects

Base path: `/api/Projects`

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM` | `CreateProjectRequest` | `ProjectResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `ProjectResponse[]` |
| GET | `/{id}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `ProjectResponse` |
| POST | `/import-word` | `PM` | multipart form-data field `file`, `.docx`, max 10 MB | imported `ProjectResponse` |
| POST | `/tasks/{taskId}/materials` | `PM` | `CreateTaskMaterialRequirementRequest` | task material requirement response/object |
| GET | `/{projectId}/material-requirements` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskMaterialResponse[]` |
| POST | `/{projectId}/mrp-runs` | `ADMIN,PM,WAREHOUSE_MANAGER` | query `warehouseId` required by service | `MRPCalculationResponse[]` |
| GET | `/{projectId}/mrp-runs/latest` | `ADMIN,PM,WAREHOUSE_MANAGER` | query `warehouseId` required | `{ planningRunId, planningVersion, projectId, warehouseId, items }` |
| POST | `/adjust-budget` | `ADMIN` | `AdjustBudgetRequest` | `ProjectResponse` or budget history object |
| GET | `/{projectId}/budget-histories` | `ADMIN,PM` | none | `ProjectBudgetHistoryResponse[]` |
| PUT | `/{projectId}` | `PM` | `UpdateProjectRequest` | updated project/status object |
| POST | `/{projectId}/start` | `PM,ADMIN` | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/pause` | `PM,ADMIN` | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/cancel` | `PM,ADMIN` | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/reopen` | `PM,ADMIN` | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| POST | `/{projectId}/complete` | `PM,ADMIN` | `ProjectLifecycleRequest` | `{ projectId, status, rowVersion }` |
| PUT | `/{projectId}/project-manager` | `ADMIN` | `ReassignProjectManagerRequest` | updated project/status object |

Project access rules:

- PMs can create only projects assigned to themselves.
- PMs can read/update/change projects they own.
- Warehouse managers can read project/MRP data only in allowed contexts; MRP requires a warehouse they manage.
- Closed projects cannot accept many downstream changes.

### Tasks

Base path: `/api/Task`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM` | `CreateTaskRequest` | `TaskResponse` |
| GET | `/project/{projectId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskResponse[]` |
| GET | `/{taskId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskResponse` |
| GET | `/project/{projectId}/material-requirements` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `TaskMaterialResponse[]` |
| GET | `/assigned` | `PM` | none | `TaskResponse[]` |
| PUT | `/{taskId}` | `PM` | `UpdateTaskRequest` | updated task/status object |
| POST | `/{taskId}/cancel` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |
| POST | `/{taskId}/reject` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |
| POST | `/{taskId}/reopen` | `PM` | `TaskLifecycleRequest` | `{ taskId, status, rowVersion }` |

### Progress Reports

Base path: `/api/ProgressReport`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `PM` | `SubmitProgressReportRequest` | `ProgressReportResponse` |
| GET | `/task/{taskId}` | `ADMIN,PM` | none | `ProgressReportResponse[]` |
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
| POST | `/` | `PM` | `CreateMaterialRequest` | `MaterialRequestResponse` |
| POST | `/task/{taskId}` | `PM` | none | `MaterialRequestResponse` generated from task planned materials |
| PUT | `/{requestId}/approve` | `WAREHOUSE_MANAGER` | `ApproveMaterialRequest` | `MaterialRequestResponse` |
| PUT | `/{requestId}/reject` | `WAREHOUSE_MANAGER` | `RejectMaterialRequest` optional | `MaterialRequestResponse` or message |
| PUT | `/{requestId}` | `PM` | `UpdatePendingMaterialRequest` | `MaterialRequestResponse` |
| PUT | `/{requestId}/cancel` | `PM` | `CancelMaterialRequest` | `MaterialRequestResponse` or message |
| PUT | `/{requestId}/issue` | `WAREHOUSE_MANAGER` | none | `MaterialRequestResponse` |
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

### Purchase Orders

Base path: `/api/PurchaseOrders`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `WAREHOUSE_MANAGER` | `CreatePurchaseOrderRequest` | `PurchaseOrderResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `PurchaseOrderResponse[]` |
| GET | `/{id}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `PurchaseOrderResponse` |
| GET | `/shortages` | `WAREHOUSE_MANAGER` | none | `ProcurementShortageResponse[]` |
| PUT | `/{id}/approve` | `ADMIN,PM` | `PurchaseOrderActionRequest` optional | `PurchaseOrderResponse` |
| PUT | `/{id}/reject` | `ADMIN,PM` | `PurchaseOrderActionRequest` optional | `PurchaseOrderResponse` |
| POST | `/from-shortages` | `WAREHOUSE_MANAGER` | `CreatePurchaseOrderRequest`; every item must include `requestItemId` | `PurchaseOrderResponse` |
| POST | `/{poId}/receive` | `WAREHOUSE_MANAGER` | `ReceivePurchaseOrderRequest` | `PurchaseOrderResponse` |
| POST | `/{poId}/ship` | `WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | `PurchaseOrderResponse` |
| POST | `/{poId}/processing` | `WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | `PurchaseOrderResponse` |
| POST | `/{poId}/cancel` | `ADMIN,PM,WAREHOUSE_MANAGER` | `PurchaseOrderActionRequest` optional | `PurchaseOrderResponse` |

Workflow:

- Warehouse manager creates POs for a warehouse they manage.
- PM/ADMIN approve/reject. PM can approve for projects they manage.
- Approval increases `onOrderQuantity`.
- Processing and shipped are warehouse-manager transitions after approval.
- Receive accepts partial receipts. Final delivery must account for every remaining unit as received, damaged, or missing.
- Delivered with variance uses `CLOSED_WITH_VARIANCE`; clean final delivery uses `DELIVERED`.

### Suppliers

Base path: `/api/Suppliers`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateSupplierRequest` | `SupplierResponse` |
| GET | `/` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `SupplierResponse[]` |
| GET | `/{supplierId}` | `ADMIN,PM,WAREHOUSE_MANAGER` | none | `SupplierResponse` |
| PUT | `/{supplierId}` | `ADMIN` | `UpdateSupplierRequest` | `SupplierResponse` or message |
| DELETE | `/{supplierId}` | `ADMIN` | none | Message |
| POST | `/recommendations/balanced` | Public by controller attribute | `BalancedSupplierRecommendationRequest` | `BalancedSupplierRecommendationResponse` |

Supplier recommendation notes:

- Internal catalog candidates are scored first using weighted cost, reliability, lead time, and material coverage.
- When `searchWebForNearbySuppliers` is `true` and `warehouseLocation` is provided, the backend uses **Tavily** to search the web, then **Gemini** to extract and score external suppliers from those search results.
- After web results are merged, **Gemini** may rerank the combined internal + external list and populate `aiSummary`.
- Response flags:
  - `usedWebSearch`: Tavily web search ran successfully and contributed external suppliers.
  - `usedGoogleAI`: Gemini reranking/summary ran successfully.
  - `webSearchSummary`: short summary of external supplier search.
  - `aiSummary`: short summary from Gemini reranking.
- External suppliers use `source: "WebSearch"` and `supplierId: 0`.
- External suppliers may include `sourceUrls[]`, `websiteUrl`, `googleMapsUrl`, `rating`, `reviewCount`, and `distanceEstimate`.

Example: balanced recommendation with web search

Request:

```json
POST /api/Suppliers/recommendations/balanced
{
  "projectId": 1,
  "items": [
    { "materialId": 10, "quantity": 50 },
    { "materialId": 12, "quantity": 20 }
  ],
  "costWeight": 40,
  "reliabilityWeight": 40,
  "leadTimeWeight": 20,
  "maxRecommendations": 5,
  "searchWebForNearbySuppliers": true,
  "warehouseLocation": "District 7, Ho Chi Minh City",
  "searchRadiusKm": 30,
  "regionCode": "VN"
}
```

Success `result` (trimmed):

```json
{
  "usedGoogleAI": true,
  "usedWebSearch": true,
  "strategy": "Balance cost, reliability, and lead time.",
  "aiSummary": "Supplier A offers the best balance...",
  "webSearchSummary": "Found 3 nearby external suppliers for cement and steel.",
  "recommendations": [
    {
      "supplierId": 4,
      "source": "InternalCatalog",
      "companyName": "BuildMart Supply",
      "balancedScore": 86.5,
      "reason": "Covers all requested materials...",
      "lines": []
    },
    {
      "supplierId": 0,
      "source": "WebSearch",
      "companyName": "Saigon Materials Co.",
      "websiteUrl": "https://example.com",
      "sourceUrls": ["https://example.com"],
      "balancedScore": 78.2,
      "reason": "External supplier found through Tavily web search."
    }
  ]
}
```

Note: the recommendation endpoint has no `[Authorize]` attribute on the controller or method in source.

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

| Method | Path | Auth | Body / Query | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `ADMIN` | `CreateWarehouseRequest` | `WarehouseResponse` |
| PUT | `/{warehouseId}` | `ADMIN` | `UpdateWarehouseRequest` | `WarehouseResponse` |
| GET | `/` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseResponse[]` |
| GET | `/{id}` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseResponse` |
| GET | `/{id}/inventory` | `ADMIN,WAREHOUSE_MANAGER` | none | `InventoryReportResponse[]` |
| GET | `/{warehouseId}/inventory/{variantId}` | `ADMIN,WAREHOUSE_MANAGER` | none | `InventoryReportResponse` |
| POST | `/inventory/adjust` | `WAREHOUSE_MANAGER` | `InventoryAdjustmentRequest` | adjustment summary object |
| GET | `/inventory/adjustments` | `ADMIN,WAREHOUSE_MANAGER` | query `status?` | adjustment summary objects |
| POST | `/inventory/adjustments/{adjustmentId}/approve` | `ADMIN` | `ReviewInventoryAdjustmentRequest` | Message |
| POST | `/inventory/adjustments/{adjustmentId}/reject` | `ADMIN` | `ReviewInventoryAdjustmentRequest` | Message |
| POST | `/inventory/return` | `WAREHOUSE_MANAGER` | `InventoryReturnRequest` | inventory return summary object |
| GET | `/inventory/transactions` | `ADMIN,WAREHOUSE_MANAGER` | query `warehouseId?`, `variantId?` | `InventoryTransactionResponse[]` |
| POST | `/physical-counts` | `WAREHOUSE_MANAGER` | `StartPhysicalCountRequest` | physical count summary object |
| POST | `/physical-counts/{sessionId}/submit` | `WAREHOUSE_MANAGER` | `SubmitPhysicalCountRequest` | `{ sessionId, status, rowVersion }` |
| POST | `/physical-counts/{sessionId}/approve` | `ADMIN` | `ReviewPhysicalCountRequest` | Message |
| POST | `/physical-counts/{sessionId}/reject` | `ADMIN` | `ReviewPhysicalCountRequest` | Message |
| GET | `/physical-counts` | `ADMIN,WAREHOUSE_MANAGER` | query `warehouseId?`, `status?` | physical count summary objects |

Inventory notes:

- Negative inventory adjustments cannot reduce stock below reserved plus quarantined quantities.
- Admin reviewers cannot approve their own adjustment/count.
- Open physical count is limited to one per warehouse.
- Returns must link to an issued or partially issued material request.

### Warehouse Transfers

Base path: `/api/WarehouseTransfers`

| Method | Path | Auth | Body | Result |
| --- | --- | --- | --- | --- |
| POST | `/` | `WAREHOUSE_MANAGER` | `CreateWarehouseTransferRequest` | `WarehouseTransferResponse`; returns HTTP 201 with `Location` |
| GET | `/` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse[]` |
| GET | `/{id}` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |
| PUT | `/{id}/approve` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |
| PUT | `/{id}/reject` | `ADMIN,WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |
| POST | `/{id}/ship` | `WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |
| POST | `/{id}/receive` | `WAREHOUSE_MANAGER` | `ReceiveWarehouseTransferRequest` optional | `WarehouseTransferResponse` |
| PUT | `/{id}/cancel` | `WAREHOUSE_MANAGER` | none | `WarehouseTransferResponse` |

### Chat

Base path: `/api/Chat`

All chat endpoints require any authenticated user.

| Method | Path | Body | Result |
| --- | --- | --- | --- |
| POST | `/conversations` | `CreateConversationRequest` | `ConversationResponse` |
| GET | `/projects/{projectId}/conversations` | none | `ConversationResponse[]` |
| GET | `/conversations/{conversationId}/messages` | none | `MessageResponse[]` |
| POST | `/conversations/{conversationId}/messages` | `SendMessageRequest` | `MessageResponse` |
| PUT | `/messages/{messageId}` | `UpdateMessageRequest` | `MessageResponse` |
| DELETE | `/messages/{messageId}` | none | `MessageResponse` for soft-deleted message |
| PUT | `/conversations/{conversationId}/read` | none | Message |

Chat rules:

- User must be a conversation participant to read/send/read-mark.
- Only sender can edit/delete a message.
- Send requires message body or attachment.
- Delete is soft delete; response includes `deletedAt`.

### AI Chat

Base path: `/api/AiChat`

All AI chat endpoints require any authenticated user.

| Method | Path | Body | Result |
| --- | --- | --- | --- |
| POST | `/sessions` | `CreateAiChatSessionRequest` | `AiChatSessionResponse` |
| GET | `/sessions` | none | `AiChatSessionResponse[]` |
| GET | `/sessions/{sessionId}/messages` | none | `AiChatMessageResponse[]` |
| POST | `/sessions/{sessionId}/messages` | `SendAiChatMessageRequest` | `AiChatReplyResponse` |
| DELETE | `/sessions/{sessionId}` | none | Message |

AI Chat rules:

- Each session is scoped to the authenticated user.
- Sessions can optionally be linked to a project; when `projectId` is set, Gemini receives project context in the system prompt.
- `POST /sessions/{sessionId}/messages` persists the user message, generates one assistant reply, and returns both in `AiChatReplyResponse`.
- Web search is optional via `useWebSearch`. When `true`, the backend runs **Tavily** for search, then **Gemini** for reasoning over the returned snippets. Gemini does not search the web directly.
- Sessions are soft-deleted by the owner via `DELETE /sessions/{sessionId}`.

AI integration architecture (backend):

| Step | Provider | Responsibility |
| --- | --- | --- |
| 1 | **Tavily** | Web search only. Returns titles, URLs, and content snippets. |
| 2 | **Gemini** | Reasoning only. Reads conversation history plus Tavily snippets and generates the answer. |

Frontend implications:

- Set `useWebSearch: true` when the user asks for current/external information (market prices, nearby suppliers, regulations, news).
- Expect slightly higher latency when web search is enabled.
- If Tavily is not configured on the backend, web-search requests return HTTP 400 with an error like `Tavily:ApiKey is not configured.`
- If Gemini is not configured, any AI chat message returns HTTP 400 with an error like `GoogleAI:ApiKey is not configured.`
- The reply includes `usedWebSearch` and `webSearchSources[]` (`title`, `url`) from Tavily. Gemini never searches the web.
- If Tavily succeeds but Gemini is rate-limited, the API returns HTTP 429 with `errorCode: "GEMINI_RATE_LIMITED"` and the Tavily sources. That is a Gemini quota issue, not a web-search failure.

Example: send AI message with web search

Request:

```json
POST /api/AiChat/sessions/50/messages
{
  "message": "What are current cement price trends in Vietnam?",
  "useWebSearch": true
}
```

Success `result`:

```json
{
  "userMessage": {
    "messageId": 201,
    "sessionId": 50,
    "role": 0,
    "content": "What are current cement price trends in Vietnam?",
    "createdAt": "2026-08-20T03:21:00Z",
    "sentAt": "2026-08-20T03:21:00Z"
  },
  "assistantMessage": {
    "messageId": 202,
    "sessionId": 50,
    "role": 1,
    "content": "Based on recent web results...",
    "createdAt": "2026-08-20T03:21:03Z",
    "sentAt": "2026-08-20T03:21:03Z"
  },
  "usedWebSearch": true,
  "webSearchSources": [
    {
      "title": "Cement market update",
      "url": "https://example.com/cement"
    }
  ]
}
```

Notes:

- `role` is enum `AiChatRole`: `0 = User`, `1 = Assistant`. If your client sends JSON enums as strings, confirm Swagger; otherwise send numeric values.
- Both `createdAt` and `sentAt` are returned for compatibility.

### Meetings

Base path: `/api/Meetings`

All meeting endpoints require any authenticated user.

| Method | Path | Body | Result |
| --- | --- | --- | --- |
| POST | `/` | `CreateMeetingRequest` | `MeetingResponse` |
| GET | `/project/{projectId}` | none | `MeetingResponse[]` |
| GET | `/{meetingId}` | none | `MeetingResponse` |
| PUT | `/{meetingId}/cancel` | `CancelMeetingRequest` | `MeetingResponse` |

Meetings can schedule with Microsoft Teams if configured. Response includes `joinUrl`, external IDs, and `failureReason`.

## Request DTOs

Auth and users:

- `UserRegisterRequest`: `email`, `password`, `confirmPassword`, `firstName`, `lastName`
- `LoginRequest`: `userEmail`, `password`
- `VerificationEmailRequest`: `userId`, `verificationCode`
- `ResendVerificationRequest`: `email`
- `RefreshSessionRequest`: `refreshToken`, `deviceInfo?`
- `LogoutRequest`: `refreshToken`
- `ForgotPasswordRequest`: `email`
- `ResetPasswordRequest`: `userId`, `token`, `newPassword`, `confirmPassword`
- `ChangePasswordRequest`: `currentPassword`, `newPassword`, `confirmPassword`
- `UpdateUserRequest`: `firstName?`, `lastName?`, `phoneNumber?`, `imgUrl?`
- `UpdateUserRoleRequest`: `role`

Project/task/progress:

- `CreateProjectRequest`: `projectName`, `address?`, `totalProjectBudget`, `startDate`, `pmUserID`, `baselineStart`, `baselineEnd`
- `UpdateProjectRequest`: `projectName`, `address?`, `startDate`, `baselineStart`, `baselineEnd`, `rowVersion`
- `ProjectLifecycleRequest`: `rowVersion`
- `ReassignProjectManagerRequest`: `projectManagerUserId`, `rowVersion`
- `AdjustBudgetRequest`: `projectId`, `amount`, `reason`
- `CreateTaskRequest`: `projectId`, `phaseName`, `taskName`, `assignedToUserID`, `plannedBudget`, `baselineStart`, `baselineEnd`, `materials[]`
- `TaskMaterialRequest`: `variantId`, `materialId`, `grossQuantityRequired`
- `UpdateTaskRequest`: `phaseName`, `taskName`, `assignedToUserID`, `plannedBudget`, `baselineStart`, `baselineEnd`, `rowVersion`
- `TaskLifecycleRequest`: `rowVersion`
- `SubmitProgressReportRequest`: `taskId`, `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`
- `ReviewProgressReportRequest`: `reviewNote?`, `allowCostOverrun`, `rowVersion`
- `CorrectProgressReportRequest`: `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`, `rowVersion`

Materials and material requests:

- `CreateCategoryRequest` / `UpdateCategoryRequest`: `categoryName`
- `MaterialRequest`: `materialName`, `defaultUnit`, `description?`, `isActive`, `categoryId`
- `UpdateMaterialRequest`: `materialName`, `defaultUnit`, `description?`, `isActive`
- `MaterialVariantRequest`: `materialId`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `color?`, `specification?`, `packaging?`, `unit`, `isActive`
- `CreateMaterialRequest`: `projectId`, `taskId?`, `warehouseId?`, `requestNote?`, `items[]`
- `MaterialItemRequest`: `variantId`, `materialId`, `quantity`, `neededByDate`, `note?`
- `CreateTaskMaterialRequirementRequest`: `variantId`, `materialId`, `grossQuantityRequired`
- `ApproveMaterialRequest`: `warehouseId`, `decisionNote?`, `items[]`
- `ApproveMaterialItemRequest`: `itemId`, `approvedQuantity`
- `RejectMaterialRequest`: `decisionNote?`
- `UpdatePendingMaterialRequest`: `rowVersion`, `requestNote?`, `items[]`
- `UpdateMaterialRequestItem`: `itemId`, `quantity`, `neededByDate`, `note?`
- `CancelMaterialRequest`: `rowVersion`, `reason?`

Procurement:

- `CreateSupplierRequest`: `companyName`, `contactEmail?`, `contactPhone?`, `address?`, `latitude?`, `longitude?`
- `UpdateSupplierRequest`: `companyName`, `contactEmail?`, `contactPhone?`
- `CreateCatalogRequest`: `supplierId`, `variantId`, `materialId`, `supplierSku?`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `UpdateCatalogRequest`: `supplierSku?`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `BalancedSupplierRecommendationRequest`: `projectId?`, `items[]`, `costWeight`, `reliabilityWeight`, `leadTimeWeight`, `maxRecommendations`, `searchWebForNearbySuppliers`, `warehouseLocation?`, `searchRadiusKm`, `regionCode?`
  - `searchWebForNearbySuppliers`: when `true`, backend uses Tavily search + Gemini extraction. Requires non-empty `warehouseLocation`.
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

- `CreateWarehouseRequest`: `managerId`, `warehouseName`, `location`
- `UpdateWarehouseRequest`: `managerId`, `warehouseName`, `location`
- `InventoryAdjustmentRequest`: `warehouseId`, `variantId`, `quantityDelta`, `reasonCode`, `note?`, `rowVersion?`
- `ReviewInventoryAdjustmentRequest`: `rowVersion`, `reviewNote?`
- `InventoryReturnRequest`: `warehouseId`, `variantId`, `quantity`, `materialRequestId`, `reasonCode`, `condition`, `note?`, `rowVersion?`
- `StartPhysicalCountRequest`: `warehouseId`, `variantIds[]`, `note?`
- `SubmitPhysicalCountRequest`: `rowVersion`, `lines[]`
- `PhysicalCountQuantityRequest`: `lineId`, `actualQuantity`
- `ReviewPhysicalCountRequest`: `rowVersion`, `reviewNote?`
- `CreateWarehouseTransferRequest`: `sourceWarehouseId`, `destinationWarehouseId`, `note?`, `items[]`
- `CreateWarehouseTransferItemRequest`: `variantId`, `quantity`
- `ReceiveWarehouseTransferRequest`: `items[]`
- `ReceiveWarehouseTransferItemRequest`: `transferItemId`, `quantity`, `damagedQuantity`, `lostQuantity`

Chat and meetings:

- `CreateConversationRequest`: `projectId`, `taskId?`, `title`, `type`, `participantUserIds[]`
- `SendMessageRequest`: `body`, `attachmentUrl?`
- `UpdateMessageRequest`: `body`
- `CreateAiChatSessionRequest`: `title?`, `projectId?`
- `SendAiChatMessageRequest`: `message`, `useWebSearch`
  - `message`: required user text.
  - `useWebSearch`: when `true`, backend calls Tavily first, then Gemini. When `false`, Gemini answers from conversation/project context only.
- `CreateMeetingRequest`: `projectId`, `taskId?`, `subject`, `agenda?`, `startDateTime`, `endDateTime`, `timeZone`, `scheduleWithTeams`, `participants[]`
- `MeetingParticipantRequest`: `userId?`, `email`, `displayName?`, `role`
- `CancelMeetingRequest`: `reason?`

## Main Response DTOs

Use these as the shape inside `result`.

- `AuthTokenResponse`: `accessToken`, `refreshToken`, `accessTokenExpiresAt`, `refreshTokenExpiresAt`
- `UserProfileResponse`: `id`, `firstName`, `lastName`, `email`, `phoneNumber`, `imgUrl?`, `role`
- `AccountResponse`: `id`, `firstName`, `lastName`, `email`, `phoneNumber`, `role`
- `ProjectResponse`: `projectId`, `projectName`, `address?`, `status`, `createdDate`, `startDate`, `baselineStart`, `baselineEnd`, `totalProjectBudget`, `budgetConfigured`, `actualCost`, `plannedTaskBudget`, `reportedTaskActualCost`, `purchaseOrderCommittedCost`, `purchaseOrderReceivedCost`, `remainingProcurementBudget`, `currency`, `pmUserID`, `pmName`, `totalTasks`, `totalAIAlerts`, `rowVersion`
- `ProjectBudgetHistoryResponse`: `id`, `projectId`, `amountChanged`, `previousBudget`, `newBudget`, `currency`, `reason`, `updatedByUserId`, `createdAt`
- `TaskResponse`: `taskId`, `projectId`, `phaseName`, `taskName`, `assignedToUserID`, `assignedToUserName`, `plannedBudget`, `actualCost`, `actualProgressPct`, `status`, `baselineStart`, `baselineEnd`, `rowVersion`, `materialRequirements[]`
- `TaskMaterialResponse`: `variantId`, `materialId`, `materialName`, `variantName`, `taskName?`, `grossQuantityRequired`, `unit`
- `ProgressReportResponse`: `reportId`, `taskId`, `taskName`, `reportedByUserId`, `reportedByName`, `reportDate`, `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`, `status`, `reviewedByUserId?`, `reviewedAt?`, `reviewNote?`, `originalReportId?`, `rowVersion`
- `CategoryResponse`: `id`, `categoryName`, `totalMaterials`
- `MaterialResponse`: `materialId`, `materialName`, `defaultUnit`, `description?`, `isActive`, `categoryId`, `variants[]`
- `MaterialVariantResponse`: `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `color?`, `specification?`, `packaging?`, `unit`, `isActive`
- `MaterialRequestResponse`: `requestId`, `projectId`, `taskId?`, `warehouseId?`, `warehouseName?`, `requestedBy`, `requestedByName`, `requestDate`, `status`, `requestNote?`, `approvedByUserId?`, `approvedAt?`, `decisionNote?`, `rowVersion`, `items[]`
- `MaterialRequisitionDetailResponse`: `itemId`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `unit?`, `quantity`, `approvedQuantity`, `issuedQuantity`, `returnedQuantity`, `netIssuedQuantity`, `remainingRequestQuantity`, `remainingTaskDemand`, `neededByDate`, `note?`
- `MRPCalculationResponse`: `variantId`, `warehouseId?`, `inventoryScope`, `materialId`, `materialName`, `variantName`, `unit`, `totalGrossRequired`, `issuedToProjectTasks`, `remainingGrossRequired`, `currentInventory`, `reservedQuantity`, `availableQuantity`, `onOrderQuantity`, `netQuantityRequired`, `earliestStartDate`, `planningRunId`, `planningVersion`, `transferRecommendations[]`
- `SupplierResponse`: `supplierId`, `companyName`, `contactEmail?`, `contactPhone?`
- `CatalogOfferResponse`: `catalogId`, `supplierId`, `supplierName`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `supplierSku?`, `unit`, `unitPrice`, `minimumOrderQuantity`, `leadTimeDays`, `isAvailable`
- `BalancedSupplierRecommendationResponse`: `usedGoogleAI`, `usedWebSearch`, `strategy`, `aiSummary?`, `webSearchSummary?`, `recommendations[]`
  - `usedGoogleAI`: Gemini was used for reranking/summary.
  - `usedWebSearch`: Tavily web search contributed external suppliers.
  - `recommendations[].source`: `InternalCatalog` or `WebSearch`.
- `PurchaseOrderResponse`: `poId`, `project`, `supplier`, `status`, `currency`, `totalAmount`, `warehouseId`, `warehouseName`, `orderDate`, `expectedDeliveryDate?`, `approvedByUserId?`, `approvedAt?`, `note?`, `rowVersion`, `items[]`
- `OrderLineItemResponse`: `orderLineItemId`, `variantId`, `materialId`, `requestItemId?`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `specification?`, `packaging?`, `unit`, `quantity`, `receivedQuantity`, `damagedQuantity`, `missingQuantity`, `accountedQuantity`, `remainingQuantity`, `unitPrice`, `subTotal`
- `ProcurementShortageResponse`: `projectId`, `projectName`, `taskId?`, `warehouseId`, `warehouseName`, `requestItemId`, `requestIds[]`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `unit`, `neededByDate`, `grossShortageQuantity`, `procurementCoverageQuantity`, `remainingShortageQuantity`, `supplierOffers[]`
- `WarehouseResponse`: `warehouseId`, `warehouseName`, `location`, `managerId`, `managerName?`, `inventoryRecords[]`, `createdDate`, `modifiedDate?`, `createdBy?`, `modifiedBy?`, `isDeleted`
- `InventoryReportResponse`: `inventoryId`, `warehouseId`, `variantId`, `materialId`, `materialName`, `variantName`, `sku?`, `brand?`, `grade?`, `size?`, `specification?`, `packaging?`, `warehouseName`, `unit`, `quantityOnHand`, `reservedQuantity`, `onOrderQuantity`, `availableQuantity`, `reorderLevel`, `quarantineQuantity`, `averageUnitCost`, `inventoryValue`, `isLowStock`, `updatedAt`, `rowVersion`
- `InventoryTransactionResponse`: `transactionId`, `warehouseId`, `variantId`, `transactionType`, `quantity`, `quantityBefore`, `quantityAfter`, `referenceId?`, `referenceType?`, `note?`, `performedByUserId`, `transactionDate`, `unitCost?`, `totalValue?`, `lotNumber?`, `batchNumber?`, `serialNumber?`, `expiryDate?`
- `WarehouseTransferResponse`: `transferId`, `sourceWarehouseId`, `sourceWarehouseName`, `destinationWarehouseId`, `destinationWarehouseName`, `status`, `requestedByUserId`, `approvedByUserId?`, `shippedByUserId?`, `receivedByUserId?`, `requestedAt`, `approvedAt?`, `shippedAt?`, `receivedAt?`, `note?`, `rowVersion`, `items[]`
- `ConversationResponse`: `conversationId`, `projectId`, `taskId?`, `title`, `type`, `lastMessageAt`, `participants[]`
- `MessageResponse`: `messageId`, `conversationId`, `senderId`, `senderName?`, `body`, `attachmentUrl?`, `sentAt`, `editedAt?`, `deletedAt?`
- `AiChatSessionResponse`: `sessionId`, `userId`, `projectId?`, `title`, `createdAt?`, `lastMessageAt?`, `messageCount`
- `AiChatMessageResponse`: `messageId`, `sessionId`, `role`, `content`, `createdAt`, `sentAt`
- `AiChatReplyResponse`: `userMessage` (`AiChatMessageResponse`), `assistantMessage` (`AiChatMessageResponse`), `usedWebSearch`, `webSearchSources[]` (`title`, `url`)
- `MeetingResponse`: `meetingId`, `projectId`, `taskId?`, `organizerId`, `organizerName?`, `subject`, `agenda?`, `startDateTime`, `endDateTime`, `timeZone`, `status`, `joinUrl?`, `externalEventId?`, `externalOnlineMeetingId?`, `failureReason?`, `participants[]`

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

Warehouse transfer status:

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

- Chat conversation type: `PROJECT`, `TASK`, `MATERIAL_REQUEST`, `PURCHASE_ORDER`
- AI chat role: `User` / `Assistant` (enum values `0` / `1` if sent as numbers)
- Meeting provider: `MICROSOFT_TEAMS`
- Meeting status: `DRAFT`, `SCHEDULED`, `FAILED`, `CANCELLED`
- Meeting participant role: `REQUIRED`, `OPTIONAL`

## AI Provider Split (Frontend Notes)

The backend uses two providers with separate responsibilities:

| Provider | Used for | Frontend trigger |
| --- | --- | --- |
| **Tavily** | Web search only | AI chat `useWebSearch: true`; supplier recommendation `searchWebForNearbySuppliers: true` + `warehouseLocation` |
| **Gemini** | Reasoning, summarization, ranking, chat replies | Always used for AI chat replies; also used after Tavily search and for supplier reranking |

Frontend UX suggestions:

- Show a "Search the web" toggle on AI chat compose UI mapped to `useWebSearch`.
- Show a loading state with two phases when web search is enabled: "Searching the web..." then "Generating answer...".
- For supplier recommendation screens, expose `searchWebForNearbySuppliers`, `warehouseLocation`, and optional `searchRadiusKm` / `regionCode`.
- Display `usedWebSearch` / `usedGoogleAI` badges on recommendation results so users know which capabilities contributed.
- Surface backend `errorMessage` directly for missing-provider configuration (`Tavily:ApiKey...`, `GoogleAI:ApiKey...`).

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

## Validation Highlights

- Passwords: 10-128 chars, at least one uppercase, lowercase, and number.
- Verification/reset token: 6 digits.
- Project: `totalProjectBudget >= 0`, baseline end must be on/after baseline start, start date must be on/before baseline end and service also requires it inside baseline period.
- Task: planned budget cannot be negative; baseline end must be on/after baseline start.
- Progress increments: greater than 0 and at most 100, 2 decimal precision; actual cost increments cannot be negative.
- Catalog available offers must have positive unit price; lead time and minimum order quantity cannot be negative.
- Create material request: `projectId > 0`, at least one item, quantities greater than 0, no duplicate variant per request.
- Approve material request: `warehouseId > 0`, at least one item, approved quantities are nonnegative and at least one should be positive.
- Create purchase order: at least one item; delivery date cannot be in the past; duplicate variants/request items are rejected; prices cannot be negative.
- Receive purchase order: each line must account for a positive amount; final delivery must account for all remaining units.
- Inventory adjustment: `quantityDelta` cannot be zero; reason must be one of inventory adjustment reasons.
- Warehouse transfer: source and destination warehouses must differ; variants cannot repeat; quantities must be positive.

## Frontend Workflow Cheat Sheet

1. Register/login through `/api/Auth`.
2. Store `accessToken`, `refreshToken`, and expiry dates from `AuthTokenResponse`.
3. Add `Authorization: Bearer <accessToken>` to protected calls.
4. On any edit screen, keep `rowVersion` from the latest read.
5. On HTTP 409, refetch the entity and let the user retry with the latest `rowVersion`.
6. For project planning: create project, create tasks, assign task material requirements, run MRP for a specific warehouse.
7. For material fulfillment: PM creates material request, warehouse manager approves/reserves, then issues or releases.
8. For procurement: warehouse manager checks shortages, creates PO, PM/ADMIN approves, warehouse manager marks processing/shipped/receives.
9. For inventory corrections: warehouse manager submits adjustment/count, ADMIN reviews.
10. For chat: create conversations per project/task and send messages as participants.
11. For AI chat: create a session, send messages with optional `useWebSearch`, render both `userMessage` and `assistantMessage` from the reply payload.
12. For AI supplier recommendations: call `/api/Suppliers/recommendations/balanced`; enable web search only when the UI collects a warehouse location.

