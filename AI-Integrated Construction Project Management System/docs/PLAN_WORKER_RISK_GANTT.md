# Frontend Implementation Guide — Work Categories, Site Workers, Risks, Gantt

Everything below is implemented and live. For each feature: what screens to build, which endpoints to call, and how to handle each response.

Locked ground rules: 1 project = 1 PM (no co-PM UI needed); Dependency Risk is out (no data exists for it); chat, AI chat, meetings, recommendations, and Word import are gone — delete those screens entirely.

---

## 1. Work categories — phase picker + display

Phases now belong to a category (Structural, Finishing, MEP, External Works, Preliminaries — ADMIN can add more).

**Endpoints**

| Call | Result |
| --- | --- |
| `GET /api/WorkCategories` (any signed-in role) | `[{ workCategoryId, name, description? }]` ordered by name — use for every picker and label |
| `POST /api/WorkCategories` (ADMIN) | body `name`, `description?` → 201 |
| `PUT /api/WorkCategories/{id}` (ADMIN) | rename/update |
| `DELETE /api/WorkCategories/{id}` (ADMIN) | 409 while any phase still uses it — show "in use, reassign phases first" |

**UI work**
- Phase create/edit forms: required category dropdown fed by `GET`. Missing/invalid id returns 400/404 — surface `errorMessage`.
- AI plan review screen: every proposed phase needs a category before `confirm` (400 otherwise). Offer the same dropdown per proposed phase.
- Everywhere a phase or task renders (lists, details, export sheets), show `workCategoryName` next to the phase name.

---

## 2. Site Worker workspace — build a minimal app, not the PM dashboard

A worker sees **only** their assigned tasks plus a minimal project header. Anything else is 403 — keep your "not shared with your role" fallback UI for those.

| Screen | Calls |
| --- | --- |
| Task list | `GET /api/Tasks/assigned` → tasks with nested `phase` (name + work category). No project filter needed — the backend scopes to the caller. |
| Task detail | `GET /api/Tasks/{taskId}` (own tasks only). Shows phase, category, dates, budgets, progress. |
| Project header | `GET /api/Projects/{id}/context` → `projectName`, `address`, `startDate`, `baselineStart`, `baselineEnd`. That's all a worker ever learns about a project — no budget, no other tasks. |
| Report history | `GET /api/ProgressReport/task/{taskId}` (own tasks) — shows PM feedback (`reviewNote`, status). |
| Submit report | `POST /api/ProgressReport` (`taskId`, `progressIncrement`, `actualCostIncrement`, `notes?`, `sitePhotoUrl?`) → report goes `PENDING`; tell the worker "sent for PM approval". Same validation as PMs (positive increment, can't exceed remaining %, one report per 15 minutes). |
| Work problems | `POST /api/Tasks/{taskId}/issues` (`description`, `photoUrl?`) → 201; `GET .../issues` lists newest-first with `OPEN`/`RESOLVED` status and reporter names. Resolving is PM-only — workers see the status flip. |

**Do not show workers:** project lists, phase lists, budgets, material requests, inventory, exports, AI tools, user admin. Gate the whole navigation on role === `WORKER`.

## 3. PM assignment UI

Task create/update forms accept `assignedToUserID`: the PM themself (or 0/blank), or a `WORKER` account — anything else returns 400 (`"Tasks may only be assigned to the owning PM or a site worker."`).

**UI work:** feed the worker picker from `GET /api/UserAccount/Workers?search=` (ADMIN/PM, same 4-field shape as the customer list). On 400, show the message and keep the form state.

## 4. PM report flow (no backend change — chain two calls)

Submit always creates `PENDING`, and self-approval is allowed. For "submit and skip the queue": call `POST /api/ProgressReport`, then immediately `POST /api/ProgressReport/{id}/approve` with the returned `reportId` + `rowVersion`. Handle the same 409s as manual approval (over-100% progress, budget overrun without `allowCostOverrun`).

---

## 5. Deleted screens — remove, don't hide

| Removed | Calls now return |
| --- | --- |
| Chat (all 7 endpoints) | **404** (code + tables deleted) |
| AI chat (all 5) | **404** (code + tables deleted) |
| Meetings (all 4) | **404** (code + tables deleted) |
| Supplier recommendations | **410** (stub kept) |
| PO writes, transfer writes, warehouse create/update | **410** (stubs kept) |
| Word import (`import-word`, `import-word-ai`) | **410** |
| Legacy `/api/task` aliases | **404** (`POST /api/task` keeps its 410 pointer) |

Delete the screens, nav items, routes, and related DTO types. Treat 404 on these paths as "feature retired" (same UX as 410).

---

## 6. Risk dashboard (ADMIN + owning PM)

`GET /api/Projects/{projectId}/risks` → `{ generatedAt, risks[] }`, each with `riskType`, `severity` (`WARNING`/`CRITICAL`), `taskId?`, `variantId?`, `message`, `metrics{}`. Empty array = all clear. Computed on demand — poll on project views, no push.

| Badge | When it fires |
| --- | --- |
| Schedule Delay | Progress > 10pp behind with deadline ≤ 14 days out (Warning); past deadline (Critical) |
| Work Item Delay | Current pace finishes after the task baseline end; or stalled with deadline ≤ 28 days out |
| Progress Deviation | Actual ≥ 25pp below time-expected progress |
| Material Shortage | Remaining task requirement exceeds available stock (Critical at zero) |
| Material Availability | Task starts within 7 days, demand still unrequested/unfulfilled |
| Budget Risk | Ledger spend ≥ 80% of budget (Warning), ≥ 100% (Critical); task actual over planned |
| Overall Project Delay | Any Critical, or ≥ 2 warnings, or latest task end past project baseline |

Render `message` + key `metrics` (e.g. `gapPct`, `shortfall`, `ratio`) in an expandable row; link `taskId` risks to the task detail.

## 7. AI recommendations (owning PM)

`POST /api/Projects/{projectId}/risks/recommend-actions` → `{ actions[] }` with `priority` (1 highest–5), `title`, `detail`, `ownerRole` (`PM`/`WAREHOUSE_MANAGER`), `relatedRiskTypes[]`. Empty array when no risks (no AI call is even made). **Nothing is applied automatically** — render as a suggestion list the PM works through manually (group or badge by `ownerRole`). 400 means the AI returned malformed output — offer "retry".

## 8. Gantt in export

The full-workbook download (`GET /api/Projects/{projectId}/export`) now contains a `Gantt` sheet after Tasks (customer workbook identical; warehouse-manager view unchanged):

- Row 1: legend + week range (truncated notice if the baseline exceeds 104 weeks).
- Row 2: `Phase / Task`, `Progress %`, `Status`, `At Risk`, then one column per week (`yyyy-MM-dd`).
- Phase header rows (shaded, bold), one row per task: green = completed, blue = in progress, gray = pending, ⚠ = at risk.

No frontend work needed beyond knowing the sheet exists — but mirror the same colors/legend if you render an on-screen Gantt.

---

## 9. Checklist

- [ ] Category picker on phase create/edit + AI review; category labels everywhere phases/tasks render.
- [ ] Worker workspace (5 read/submit calls above) with role-gated navigation; graceful 403 fallback kept.
- [ ] PM worker picker on task forms with 400 handling.
- [ ] Report submit + issue report/resolve flows; PM chained submit→approve where "skip approval" is wanted.
- [ ] Deleted screens removed (chat, AI chat, meetings, recommendations, imports); 404/410 treated as retired.
- [ ] Risk dashboard with badges, metrics, empty state, and polling.
- [ ] AI suggestion list (manual execution only).
- [ ] Export includes Gantt; no UI change required.
