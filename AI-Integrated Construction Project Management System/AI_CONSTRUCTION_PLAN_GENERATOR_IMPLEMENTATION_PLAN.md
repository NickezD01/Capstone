# AI Construction Plan Generator Implementation Plan

## Goal

Build an AI API feature that guides a user through exactly five planning questions, then returns a strict JSON result that the frontend or backend can use to generate a detailed Excel construction plan.

The feature should fit the existing BuildSense backend architecture:

- ASP.NET Core API controllers in `cpms_API/Controllers`
- Application services in `cpms_Application/Services`
- Request DTOs in `cpms_Application/Request`
- Response DTOs in `cpms_Application/Response`
- Shared `ApiResponse` envelope
- Existing Gemini integration through `IGoogleAIClient`

## User Flow

1. User opens the AI construction planner.
2. Frontend calls the API to get the five questions.
3. User answers all five questions.
4. Frontend submits the answers to the planner API.
5. Backend validates the answers and sends them to Gemini with a strict JSON-only instruction.
6. Backend parses and validates the AI JSON.
7. Backend returns the normalized JSON in the standard API envelope.
8. Excel generation uses the returned JSON to create a multi-sheet construction plan workbook.

## Five Questions

The questions should collect enough information to produce a practical first-pass construction plan without overwhelming the user.

| No. | Field | Question | Purpose |
| --- | --- | --- | --- |
| 1 | `projectOverview` | What type of construction project do you want to build, and what is the target size or scope? | Defines project type, scale, and major assumptions. |
| 2 | `locationAndSite` | Where is the project located, and are there any important site conditions or constraints? | Drives weather, access, logistics, labor, and regulatory assumptions. |
| 3 | `timeline` | What is the target start date and desired completion date or duration? | Allows schedule, phase, milestone, and dependency planning. |
| 4 | `budgetAndQuality` | What is the estimated budget, currency, and expected quality level? | Guides cost allocation, contingency, material selection, and procurement strategy. |
| 5 | `specialRequirements` | What special requirements should the plan include, such as permits, sustainability, safety, suppliers, or risk concerns? | Captures constraints that affect work breakdown, risk, compliance, and procurement. |

## Recommended API Design

Base path: `/api/AiConstructionPlanner`

All endpoints should require `Authorization: Bearer <accessToken>` because generated plans may contain project, budget, and site details.

| Method | Path | Purpose |
| --- | --- | --- |
| `GET` | `/questions` | Return the five planner questions and field metadata. |
| `POST` | `/generate-json` | Submit the five answers and return Excel-ready JSON. |
| `POST` | `/generate-excel` | Optional later endpoint that returns an `.xlsx` file from the generated JSON. |

### GET `/api/AiConstructionPlanner/questions`

Response:

```json
{
  "statusCode": 200,
  "isSuccess": true,
  "errorMessage": null,
  "result": {
    "version": "1.0",
    "questions": [
      {
        "order": 1,
        "field": "projectOverview",
        "label": "What type of construction project do you want to build, and what is the target size or scope?",
        "required": true,
        "placeholder": "Example: 3-floor residential house, 250 m2 total floor area"
      }
    ]
  }
}
```

### POST `/api/AiConstructionPlanner/generate-json`

Request:

```json
{
  "projectId": 1,
  "answers": {
    "projectOverview": "3-floor residential house, 250 m2 total floor area",
    "locationAndSite": "District 7, Ho Chi Minh City; narrow alley access; flat site",
    "timeline": "Start 2026-10-01, finish within 8 months",
    "budgetAndQuality": "3.5 billion VND, mid-high quality",
    "specialRequirements": "Include permits, safety plan, sustainable materials, and supplier planning"
  }
}
```

Fields:

| Field | Type | Required | Notes |
| --- | --- | --- | --- |
| `projectId` | integer/null | no | Optional link to an existing BuildSense project. If provided, verify access. |
| `answers.projectOverview` | string | yes | 20-2000 characters. |
| `answers.locationAndSite` | string | yes | 10-2000 characters. |
| `answers.timeline` | string | yes | 10-1000 characters. |
| `answers.budgetAndQuality` | string | yes | 10-1000 characters. |
| `answers.specialRequirements` | string | yes | 0-2000 characters. Allow `"None"` when not applicable. |

Success response:

```json
{
  "statusCode": 200,
  "isSuccess": true,
  "errorMessage": null,
  "result": {
    "planId": "generated-client-id-or-guid",
    "version": "1.0",
    "generatedAt": "2026-09-01T00:00:00Z",
    "projectSummary": {},
    "excelSheets": {}
  }
}
```

Failure response examples:

```json
{
  "statusCode": 400,
  "isSuccess": false,
  "errorMessage": "All five planning answers are required.",
  "result": {
    "errorCode": "PLANNER_ANSWERS_INCOMPLETE"
  }
}
```

```json
{
  "statusCode": 400,
  "isSuccess": false,
  "errorMessage": "AI returned invalid planner JSON. Please try again.",
  "result": {
    "errorCode": "AI_JSON_INVALID"
  }
}
```

## Excel-Ready JSON Contract

The AI must return only valid JSON. No Markdown, comments, explanations, or code fences.

Top-level shape:

```json
{
  "planId": "string",
  "version": "1.0",
  "generatedAt": "ISO-8601 UTC datetime",
  "projectSummary": {
    "projectName": "string",
    "projectType": "string",
    "location": "string",
    "scope": "string",
    "assumptions": ["string"],
    "currency": "string",
    "estimatedBudget": 0,
    "targetStartDate": "YYYY-MM-DD or null",
    "targetEndDate": "YYYY-MM-DD or null",
    "estimatedDurationDays": 0
  },
  "excelSheets": {
    "overview": [],
    "phases": [],
    "tasks": [],
    "materials": [],
    "labor": [],
    "equipment": [],
    "costPlan": [],
    "procurementPlan": [],
    "riskRegister": [],
    "permitChecklist": [],
    "safetyPlan": [],
    "milestones": []
  }
}
```

### Sheet: `overview`

Each row should be a key/value pair for simple Excel rendering.

```json
[
  {
    "section": "Project",
    "item": "Project Type",
    "value": "Residential house",
    "notes": "Generated from user answers"
  }
]
```

### Sheet: `phases`

```json
[
  {
    "phaseId": "P01",
    "phaseName": "Pre-construction",
    "description": "Permits, design finalization, procurement setup",
    "startWeek": 1,
    "endWeek": 4,
    "durationDays": 28,
    "estimatedCost": 0,
    "dependencies": [],
    "deliverables": ["Approved design", "Permit package"]
  }
]
```

### Sheet: `tasks`

```json
[
  {
    "taskId": "T001",
    "phaseId": "P01",
    "taskName": "Submit building permit application",
    "description": "Prepare and submit required documents to local authority",
    "startWeek": 1,
    "endWeek": 2,
    "durationDays": 10,
    "predecessorTaskIds": [],
    "responsibleRole": "Project Manager",
    "estimatedCost": 0,
    "priority": "High",
    "acceptanceCriteria": "Permit application submitted with all required documents"
  }
]
```

### Sheet: `materials`

```json
[
  {
    "materialId": "M001",
    "phaseId": "P02",
    "materialName": "Ready-mix concrete",
    "specification": "C30 or locally appropriate equivalent",
    "estimatedQuantity": 0,
    "unit": "m3",
    "unitCost": 0,
    "totalCost": 0,
    "neededByWeek": 6,
    "notes": "Validate quantity with structural drawings"
  }
]
```

### Sheet: `labor`

```json
[
  {
    "laborId": "L001",
    "phaseId": "P02",
    "role": "Site supervisor",
    "estimatedHeadcount": 1,
    "durationDays": 60,
    "dailyRate": 0,
    "totalCost": 0,
    "notes": "Required during structural phase"
  }
]
```

### Sheet: `equipment`

```json
[
  {
    "equipmentId": "E001",
    "phaseId": "P02",
    "equipmentName": "Concrete mixer or pump",
    "quantity": 1,
    "durationDays": 5,
    "dailyRate": 0,
    "totalCost": 0,
    "notes": "Depends on site access"
  }
]
```

### Sheet: `costPlan`

```json
[
  {
    "costCode": "C001",
    "category": "Materials",
    "description": "Structural concrete and reinforcement",
    "estimatedAmount": 0,
    "percentageOfBudget": 0,
    "contingencyAmount": 0,
    "notes": "AI estimate, validate before approval"
  }
]
```

### Sheet: `procurementPlan`

```json
[
  {
    "procurementId": "PR001",
    "itemName": "Rebar",
    "sourceType": "Supplier",
    "requiredByWeek": 5,
    "leadTimeDays": 14,
    "orderByWeek": 3,
    "estimatedCost": 0,
    "riskLevel": "Medium",
    "notes": "Confirm current market price"
  }
]
```

### Sheet: `riskRegister`

```json
[
  {
    "riskId": "R001",
    "riskCategory": "Schedule",
    "riskDescription": "Permit approval delay",
    "probability": "Medium",
    "impact": "High",
    "mitigationPlan": "Submit early and track authority feedback weekly",
    "ownerRole": "Project Manager"
  }
]
```

### Sheet: `permitChecklist`

```json
[
  {
    "permitId": "PER001",
    "permitName": "Building permit",
    "required": true,
    "targetSubmissionWeek": 1,
    "targetApprovalWeek": 4,
    "responsibleRole": "Project Manager",
    "notes": "Verify local authority requirements"
  }
]
```

### Sheet: `safetyPlan`

```json
[
  {
    "safetyId": "S001",
    "activity": "Excavation",
    "hazard": "Collapse or underground utility strike",
    "controlMeasure": "Survey utilities, shore excavation, restrict access",
    "inspectionFrequency": "Daily",
    "responsibleRole": "Site Supervisor"
  }
]
```

### Sheet: `milestones`

```json
[
  {
    "milestoneId": "MS001",
    "milestoneName": "Foundation complete",
    "targetWeek": 8,
    "relatedPhaseId": "P02",
    "completionCriteria": "Foundation works inspected and accepted"
  }
]
```

## AI Prompt Strategy

Create a dedicated system instruction for construction planning:

```text
You are BuildSense AI Construction Planner. Generate a practical construction plan from five user answers.
Return only valid JSON matching the required schema. Do not include Markdown or explanations.
Use conservative assumptions when details are missing. Mark uncertain values in notes or assumptions.
Costs are planning estimates only and must be validated by professionals before execution.
```

Create the user prompt from the request DTO:

```text
Generate an Excel-ready construction plan JSON.

Question 1 - Project overview:
{projectOverview}

Question 2 - Location and site:
{locationAndSite}

Question 3 - Timeline:
{timeline}

Question 4 - Budget and quality:
{budgetAndQuality}

Question 5 - Special requirements:
{specialRequirements}

Return JSON with these sheet arrays:
overview, phases, tasks, materials, labor, equipment, costPlan, procurementPlan, riskRegister, permitChecklist, safetyPlan, milestones.
```

Backend should still validate the returned JSON because AI output is not trusted.

## Backend Implementation Steps

### 1. Add request DTOs

Create folder:

`cpms_Application/Request/AiConstructionPlanner`

Add:

- `GenerateConstructionPlanRequest`
- `ConstructionPlanAnswersRequest`

Suggested properties:

- `int? ProjectId`
- `ConstructionPlanAnswersRequest Answers`
- `string ProjectOverview`
- `string LocationAndSite`
- `string Timeline`
- `string BudgetAndQuality`
- `string SpecialRequirements`

### 2. Add response DTOs

Create folder:

`cpms_Application/Response/AiConstructionPlanner`

Add strongly typed DTOs for:

- `ConstructionPlannerQuestionResponse`
- `ConstructionPlannerQuestionsResponse`
- `ConstructionPlanJsonResponse`
- `ConstructionProjectSummaryResponse`
- One row DTO per Excel sheet

Use typed DTOs instead of returning raw `JsonElement` so Swagger and frontend TypeScript generation stay predictable.

### 3. Add service interface

Create:

`cpms_Application/Interfaces/IAiConstructionPlannerService.cs`

Methods:

```csharp
Task<ApiResponse> GetQuestionsAsync();
Task<ApiResponse> GeneratePlanJsonAsync(GenerateConstructionPlanRequest request);
```

### 4. Add service implementation

Create:

`cpms_Application/Services/AiConstructionPlannerService.cs`

Responsibilities:

- Validate all five answers.
- If `projectId` is provided, verify the project exists and the current user can access it.
- Build deterministic system instruction and prompt.
- Call `IGoogleAIClient.GenerateTextAsync`.
- Strip accidental Markdown fences if needed.
- Parse with `System.Text.Json`.
- Validate required top-level fields and sheet arrays.
- Normalize generated date/time values.
- Return `ApiResponse.SetOk(plan)`.

### 5. Add controller

Create:

`cpms_API/Controllers/AiConstructionPlannerController.cs`

Controller shape:

```csharp
[Route("api/[controller]")]
[ApiController]
[Authorize]
public class AiConstructionPlannerController : ControllerBase
{
    [HttpGet("questions")]
    public async Task<IActionResult> GetQuestions()

    [HttpPost("generate-json")]
    public async Task<IActionResult> GenerateJson([FromBody] GenerateConstructionPlanRequest request)
}
```

Return status with:

```csharp
return StatusCode((int)response.StatusCode, response);
```

### 6. Register dependency injection

In `cpms_API/Program.cs`, add:

```csharp
builder.Services.AddScoped<IAiConstructionPlannerService, AiConstructionPlannerService>();
```

### 7. Add validation

Add FluentValidation validator in `cpms_Application/Validators` or validate inside the service to match existing patterns.

Rules:

- `Answers` is required.
- `ProjectOverview`, `LocationAndSite`, `Timeline`, and `BudgetAndQuality` are required.
- `SpecialRequirements` can be empty only if normalized to `"None"`.
- Reject answers that are too short to be useful.
- Limit long fields to reduce prompt cost.

### 8. Add optional Excel generation

The first implementation can return JSON only. The next endpoint can generate `.xlsx`.

Recommended library options:

- `ClosedXML` for server-side `.xlsx` generation in .NET.
- Frontend Excel generation with `xlsx` if the frontend already owns downloads.

Preferred backend endpoint:

`POST /api/AiConstructionPlanner/generate-excel`

Input:

```json
{
  "plan": {}
}
```

Output:

- Content type: `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet`
- File name: `construction-plan-{yyyyMMdd-HHmm}.xlsx`

Workbook sheets:

- Overview
- Phases
- Tasks
- Materials
- Labor
- Equipment
- Cost Plan
- Procurement Plan
- Risk Register
- Permit Checklist
- Safety Plan
- Milestones

## Frontend Implementation Notes

Add a planner screen with:

- Five question form fields.
- Optional project selector.
- Generate button.
- Loading state: `Generating plan...`
- Error display using `errorMessage`.
- JSON preview for debugging.
- Download Excel action after JSON generation.

Frontend TypeScript API functions:

- `getAiConstructionPlannerQuestions()`
- `generateAiConstructionPlanJson(request)`
- `generateAiConstructionPlanExcel(plan)`

## Security and Safety

- Require authentication.
- Do not store generated plans unless a separate save feature is explicitly added.
- Do not let user input override system instructions.
- Treat AI JSON as untrusted until parsed and validated.
- Make clear in UI that generated costs and schedules are planning estimates.
- Do not claim legal, engineering, or permit certainty. Use notes that require local professional validation.

## Testing Plan

Unit tests:

- Returns exactly five questions.
- Rejects missing answers.
- Rejects incomplete answer objects.
- Handles Gemini missing API key.
- Handles Gemini rate limit.
- Handles invalid AI JSON.
- Parses valid AI JSON into typed response.
- Verifies required Excel sheet arrays exist.

Controller tests:

- `GET /questions` requires auth.
- `POST /generate-json` requires auth.
- `POST /generate-json` returns standard `ApiResponse`.
- Invalid request returns HTTP 400.

Integration/manual tests:

- Submit realistic residential project answers.
- Submit commercial renovation answers.
- Submit tight budget/short timeline answers.
- Confirm generated JSON can be converted into an Excel workbook.
- Confirm Swagger shows request and response DTOs clearly.

## Acceptance Criteria

- API returns five questions from `GET /api/AiConstructionPlanner/questions`.
- API accepts answers through `POST /api/AiConstructionPlanner/generate-json`.
- Response always uses the shared `ApiResponse` envelope.
- Successful response contains valid JSON with all required workbook sheet arrays.
- Invalid user input returns clear HTTP 400 errors.
- Invalid AI output is caught and returned as a controlled error.
- Feature reuses `IGoogleAIClient`; no second AI provider integration is introduced.
- The JSON contract is stable enough for frontend or backend Excel generation.

## Suggested Delivery Order

1. Implement questions endpoint and DTOs.
2. Implement generate-json service with strict prompt and parsing.
3. Add validation and tests for success/failure paths.
4. Add frontend form and JSON preview.
5. Add Excel export using the stable JSON contract.
6. Add optional save-to-project feature later if needed.
