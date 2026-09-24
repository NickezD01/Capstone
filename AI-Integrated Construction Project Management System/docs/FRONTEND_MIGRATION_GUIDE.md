# Frontend Migration Guide — Backend Restructuring (Steps 1–15)

Copy this file into the frontend repo. It describes every backend change that affects the UI, what to change, and the HTTP contracts to code against.

Rule of thumb for status codes:

| Code | Meaning for the frontend |
| --- | --- |
| `410 Gone` | Feature retired. Hide/remove the UI. Do not retry. |
| `403 Forbidden` | Signed in, but this role/user may not do this. Hide the action. |
| `409 Conflict` | Stale `rowVersion`, duplicate name, budget cap, or state rule. Refetch and let the user retry. |
| `400 BadRequest` | Validation. Show `errorMessage`. |

---

## 1. Roles — what each role can do now

`ADMIN` is **read-only** for project/phase/task/inventory business actions. It keeps: user/account administration, categories, materials, suppliers, catalogs, project-manager reassignment, and all reads.

| Area | ADMIN | PM | WAREHOUSE_MANAGER | CUSTOMER |
| --- | --- | --- | --- | --- |
| Project lifecycle (start/pause/cancel/reopen/complete) | ❌ (was allowed) | ✅ owning PM only | ❌ | ❌ |
| Project budget adjustment | ❌ (was allowed) | ✅ owning PM only | ❌ | ❌ |
| Project create/update/customer assign | ❌ | ✅ own only | ❌ | ❌ |
| Project reads | ✅ | ✅ own only | ✅ linked only | ✅ assigned only |
| MRP run (`POST mrp-runs`) | ❌ (was allowed) | ✅ own only | ✅ must manage active warehouse | ❌ |
| Phase/task create/update/lifecycle | ❌ | ✅ own only | ❌ | ❌ |
| Inventory adjust/return/count create + submit | ❌ | ❌ | ✅ | ❌ |
| Inventory adjustment + physical-count review | ❌ (was allowed) | ❌ | ✅ must manage the warehouse; **self-review allowed** | ❌ |
| Material request create/update/cancel | ❌ | ✅ own only | ❌ | ❌ |
| Material request approve/reject/issue/release + actual-cost | ❌ | ❌ | ✅ | ❌ |
| Project export | ✅ full report | ✅ own, full | ✅ linked, inventory view | ✅ assigned, full |
| AI plan preview/confirm | ❌ | ✅ own only | ❌ | ❌ |
| AI planner `generate-json`/`generate-excel` | ✅ staff | ✅ staff | ✅ staff | ❌ |
| Purchase-order writes, chat, AI chat, recommendations | ❌ removed | ❌ removed | ❌ removed | ❌ removed |
| Meetings | ❌ removed (code + tables dropped) | ❌ removed | ❌ removed | ❌ removed |
| Supplier/catalog admin CRUD | ✅ (reference data only) | ❌ | ❌ | ❌ |

`SUPPLIER` is a **retired role**: existing supplier accounts are locked out, `ADMIN` can no longer assign it — remove it from role dropdowns.

**Account provisioning (ADMIN only, no self-registration):** `POST /api/Auth/register`, `/Verification`, and `/resend-verification` return **410**. Replace the chained register → update-role flow with the single `POST /api/UserAccount` (`firstName`, `lastName`, `email`, `phoneNumber?`, `role` except `SUPPLIER`, `password` + `confirmPassword`); the account is verified immediately and the response carries the new user id. Email is now password-reset only.

**Frontend changes:**
- Hide project lifecycle buttons, budget-adjust buttons, and MRP-run buttons for `ADMIN` (they now 403/are PM-only).
- Show inventory review (approve/reject) buttons to `WAREHOUSE_MANAGER`, including on their own submissions (self-review is allowed).
- Remove warehouse create/edit screens, transfer create/approve/ship/receive screens, all chat screens, all AI-chat screens, recommendation screens, and all purchase-order write screens (keep PO history views).
- Remove warehouse dropdowns everywhere (see §2).

## 1b. Site Worker (new restricted role)

A `WORKER` sees only what they're assigned to — build them a minimal workspace, not the PM dashboard:

| Worker can access | Notes |
| --- | --- |
| `GET /api/Tasks/assigned` | Their task list (includes nested phase + work category) |
| `GET /api/Tasks/{taskId}` | Own tasks only; others 403 |
| `GET /api/Projects/{id}/context` | Minimal header only: name, address, start/end dates. No budget, no other tasks |
| `POST /api/ProgressReport` | Submit for own tasks (still needs PM approval) |
| `GET /api/ProgressReport/task/{taskId}` | Reports on own tasks (PM feedback) |

PM side: task create/update now accept `assignedToUserID` — must be the PM themself (or 0) or a `WORKER`, anything else is 400. Feed the worker picker from `GET /api/UserAccount/Workers?search=` (ADMIN/PM).

Work problems: workers (and PMs) report via `POST /api/Tasks/{taskId}/issues`; list via `GET .../issues`; PM resolves via `PUT /api/Tasks/issues/{issueId}/resolve` with `rowVersion`. Show open issues on the task view with reporter names and timestamps.

---

## 2. Single active warehouse — no warehouse selection

There is exactly one active warehouse. Warehouse selection no longer exists.

- `POST /api/Warehouses` and `PUT /api/Warehouses/{id}` → **410**.
- Transfer writes → **410** (transfer reads stay for history).
- The `warehouseId` fields were **removed** from `CreateMaterialRequest`, `ApproveMaterialRequest`, `InventoryAdjustmentRequest`, `InventoryReturnRequest`, and `StartPhysicalCountRequest`. Stop sending them (extra JSON props are ignored, but they no longer exist).
- All inventory operations resolve the active warehouse server-side.
- `WarehouseResponse` includes `isActive`.

**Frontend changes:** delete every warehouse picker (adjust/return/count/request forms). Show the active warehouse as read-only context.

---

## 3. Projects, phases, tasks

- Every task belongs to a phase (`PhaseId` required). Create tasks only via `POST /api/Phases/{phaseId}/tasks`.
- `POST /api/task` → **410**. The lowercase `/api/task` route aliases were **removed** — use only:
  - `GET /api/Projects/{projectId}/tasks`
  - `GET /api/Tasks/{taskId}`, `GET /api/Tasks/assigned`
  - `PUT /api/Tasks/{taskId}`, task lifecycle under `/api/Tasks/{taskId}/...`
  - `GET /api/Projects/{projectId}/material-requirements`
- `TaskResponse` carries `phaseId`, `phaseName`, and nested `phase`. Task dates must sit inside both project and phase baselines.
- Phase endpoints: `POST /api/Projects/{projectId}/phases`, `GET .../phases`, `GET /api/Phases/{phaseId}`, `PUT /api/Phases/{phaseId}`, `POST /api/Phases/{phaseId}/cancel` (all owning-PM writes; reads are staff-scoped). Project structure is phase → task, nothing in between.
- Project customer: `PUT /api/Projects/{projectId}/customer` (`customerUserId?`, `rowVersion`, owning PM). `ProjectResponse.customerName` exists. A `CUSTOMER` sees only assigned projects. Populate the picker with `GET /api/UserAccount/Customers?search=` (PM-visible).
- Visibility grants: assigned customers may list `GET .../phases` and `GET .../tasks` of their projects (detail endpoints stay staff-only); warehouse managers with operational project access may read `GET /api/ProgressReport/task/{taskId}`. Other roles still get 403 — keep the graceful "not shared" notes as fallback.
- AI import alternative: `POST /api/Projects/import-word-ai` (multipart `.docx`, PM) returns a draft (`project` fields + `plan` preview with temp IDs, persists nothing). Show it for review, create the project normally, then send the preview to `.../ai/confirm`.

**Frontend changes:** migrate every `/api/task/...` call to the canonical routes above; render phase grouping from the nested `phase` object; add assign/clear-customer UI (PM) with `rowVersion` handling.

---

## 4. Material requests + issue-time budget ledger

New fields on `MaterialRequestResponse`: `estimatedCost`, `actualCost`, `budgetDebitedAmount`, `actualCostUpdatedAt?`, `actualCostUpdatedByUserId?`. Per line: `unitActualCost`.

| Action | Contract |
| --- | --- |
| Create (PM) | `CreateMaterialRequest` now takes `estimatedCost` (≥ 0, planning only — never debits). |
| Update pending (PM) | `UpdatePendingMaterialRequest` accepts optional `estimatedCost`. Needs `rowVersion`. |
| Approve (WM) | Items accept optional `unitActualCost`. |
| Issue (WM) | Optional body `IssueMaterialRequest` (`rowVersion?`, `items[]` with `itemId` + `quantity` for partial issue; omit `items` to issue everything reserved). `PartiallyIssued` requests can be re-issued. Re-issuing a fully issued request → 409. Blocked with 409 if spend would exceed `totalProjectBudget`, on negative cost, or stale `rowVersion`. |
| Adjust actual cost (WM, new) | `PUT /api/MaterialRequest/{requestId}/actual-cost` with `AdjustActualCostRequest` (`rowVersion`, `note?`, `items[itemId, unitActualCost]`). Only the delta on outstanding quantity posts. |
| Return (WM) | Posts an explicit reversal; restores debited amount and task actuals. |
| Release (WM) | Never touches the budget. |

**Frontend changes:**
- Add an estimate input to request creation (default 0 is fine for task-generated requests).
- Show estimate / actual / debited on request detail and per-line unit costs.
- Add a WM "adjust actual cost" form (per-line unit costs + note); send the request `rowVersion`.
- Support partial-issue quantities; after a partial issue, offer "issue remainder".
- Handle 409 (budget cap, re-issue, stale version) by refetching and showing `errorMessage`.

---

## 5. AI project planning (owning PM only)

- Hardcode your own question form (project type, floor area, floors, start date, end date, budget?, special requirements?) and send `AiProjectBriefRequest` as `brief` — the backend maps it onto the same AI contract. Legacy `answers` still work; `brief` wins when both are sent.
- `POST /api/Projects/{projectId}/ai/phases:generate` (`brief`) → preview with `phases` (temporary IDs like `PH-P01`).
- `POST /api/Projects/{projectId}/ai/tasks:generate` (`brief` + `phaseId` **or** `phases` echo) → preview with `phases` + `tasks` (tasks reference phases by stable `aiKey`, so renames don't break links).
- `POST /api/Projects/{projectId}/ai/confirm` (final `phases` + `tasks`) → HTTP 201 with temp-ID → real-ID mapping. Nothing persists until confirm.
- `POST /api/Projects/{projectId}/ai/complete` (`brief` + `focusNote?`) → "AI finish the planning": preview of only the remaining work given what's already built. Check `warnings` (skipped duplicates), strip reference entries (empty `TempId`) before `confirm`.
- The generic five-question planner (`generate-json`/`generate-excel`) is unchanged (staff only).

**Frontend changes:** build a review screen — show the preview, allow rename/re-date/re-budget/removal, keep temp IDs stable, send the edited proposal to `confirm`.

---

## 6. Project export (new)

`GET /api/Projects/{projectId}/export` downloads an `.xlsx` built from live data. 403 when the caller can't access the project.

| Caller | Sheets |
| --- | --- |
| ADMIN, owning PM, assigned CUSTOMER (identical for PM/customer) | Project, Phases, Tasks, Gantt, Material Requests, Request Lines, Budget Ledger, Budget Summary, Progress |
| Linked WAREHOUSE_MANAGER | Project, Material Requests, Request Lines, Inventory, Stock Movements |

**Frontend changes:** add a download button; expect different sheets per role; no user/account data is ever included.

---

## 6b. Project risks (new)

`GET /api/Projects/{projectId}/risks` (ADMIN, owning PM) returns a computed, never-persisted scan. Empty `risks[]` means all clear. Dependency risk is intentionally excluded (no dependency data exists).

| Risk | Triggers |
| --- | --- |
| Schedule Delay | Progress gap > 10pp with deadline ≤ 14 days out (Warning), or past deadline (Critical) |
| Work Item Delay | Current pace finishes after the task baseline end, or no progress with deadline ≤ 28 days out |
| Progress Deviation | Actual ≥ 25pp below time-expected progress |
| Material Shortage | Remaining task requirement exceeds available stock (Critical when nothing available) |
| Material Availability | Task starts within 7 days with demand still unrequested or unfulfilled |
| Budget Risk | Ledger spend ≥ 80% of budget (Warning), ≥ 100% (Critical); task actual over planned |
| Overall Project Delay | Any Critical, or ≥ 2 warnings, or latest task end past project baseline |

**Frontend changes:** risk dashboard/badge from this endpoint; render `message` + `metrics`; poll on project views (no push notifications). For AI-recommended actions, call `POST .../risks/recommend-actions` (owning PM) and render the suggestion list — actions are never applied automatically.

---

## 7. Retired endpoints (all 410)

| Gone | Notes |
| --- | --- |
| `POST/PUT/DELETE /api/Warehouses` (create/update), all transfer writes | Single-warehouse model |
| All purchase-order writes (create, approve, reject, from-shortages, receive, ship, processing, cancel) | Reads + shortages stay for history |
| `POST /api/Suppliers/recommendations/balanced` | Was public |
| All `/api/Chat` and `/api/AiChat` endpoints (writes and reads) | Remove chat UIs entirely |
| Legacy `/api/task` aliases | `POST /api/task` keeps its 410 pointer |

Kept as-is: supplier/catalog admin CRUD + staff reads, transfer/PO reads, inventory reads. Chat/AI-chat/meeting code and tables were deleted outright (not just 410) — remove every reference including history views.

---

## 8. Frontend checklist

- [ ] Role guards updated per §1 (ADMIN read-only; PM-only lifecycle/budget; WM inventory reviews incl. self-review).
- [ ] `SUPPLIER` removed from role dropdowns; supplier-registration UI (if any) removed.
- [ ] Staff-users page uses single `POST /api/UserAccount` (verified immediately); register/verify screens removed; 410 handled as "feature retired".
- [ ] Warehouse pickers removed; warehouse create/edit/transfer-write UIs removed.
- [ ] All `/api/task/...` calls migrated to canonical routes.
- [ ] Estimate input + estimate/actual/debited display on material requests.
- [ ] WM actual-cost adjustment form + partial-issue flow + 409 handling.
- [ ] AI preview → edit → confirm flow (stable temp IDs).
- [ ] Export download button with role-aware sheet expectations.
- [ ] Retired screens removed (PO writes, chat, AI chat, recommendations, meetings); 410 handled as "feature retired", deleted routes as 404.
- [ ] Worker workspace (assigned tasks, project header, report submit) + PM worker picker on task forms.
- [ ] Risk dashboard: call `GET /api/Projects/{id}/risks` (ADMIN/owning PM), render `WARNING`/`CRITICAL` badges with messages + metrics; empty list means all clear.
- [ ] `rowVersion` round-trip on every edit screen; 409 → refetch → retry.
