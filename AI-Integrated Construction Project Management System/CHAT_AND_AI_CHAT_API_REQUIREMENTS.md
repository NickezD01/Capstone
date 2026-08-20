# Chat and AI Chat API Requirements

This is the backend handoff for the frontend chat work. The UI is wired to these endpoints through `src/api/chat.ts` and `src/api/aiChat.ts`.

## Shared API Envelope

Every endpoint should return the existing BuildSense envelope:

```json
{
  "statusCode": 200,
  "isSuccess": true,
  "errorMessage": null,
  "result": {}
}
```

Validation or permission failures should keep the same shape with `isSuccess: false`, a useful `errorMessage`, and the real HTTP status code.

### Error Response Contract

The frontend should always check `isSuccess` first. When `isSuccess` is `false`, use `errorMessage` for display text and use `result.errorCode` for programmatic handling when it is present.

Database failures should not return raw SQL exception text. They should return the shared envelope with a stable machine-readable error code:

```json
{
  "statusCode": 503,
  "isSuccess": false,
  "errorMessage": "The database schema is not ready. Run the latest backend migrations.",
  "result": {
    "errorCode": "DATABASE_SCHEMA_NOT_READY",
    "retryable": false
  }
}
```

Known database error codes:

| HTTP | `result.errorCode`                 | Meaning for frontend handling                                                                  |
| ---- | ---------------------------------- | ---------------------------------------------------------------------------------------------- |
| 503  | `DATABASE_SCHEMA_NOT_READY`        | The API is running against a database that has not applied the latest migrations. Do not retry. |
| 503  | `DATABASE_UNAVAILABLE`             | The database connection/login/server is temporarily unavailable. Retry can be offered.          |
| 400  | `DATABASE_CONSTRAINT_VIOLATION`    | The submitted ID or relationship is invalid, stale, or violates a quantity constraint.          |
| 409  | `DATABASE_DUPLICATE_KEY`           | The request conflicts with an existing unique record.                                           |
| 500  | `DATABASE_ERROR`                   | Unclassified database save error. Log/report and show a generic failure message.                |

## Team Chat

Base path: `/api/Chat`

All endpoints require `Authorization: Bearer <accessToken>`.

### Required Endpoints

| Method   | Path                                                | Purpose                                               |
| -------- | --------------------------------------------------- | ----------------------------------------------------- |
| `POST`   | `/api/Chat/conversations`                           | Start a conversation.                                 |
| `GET`    | `/api/Chat/projects/{projectId}/conversations`      | List conversations for the current user in a project. |
| `GET`    | `/api/Chat/conversations/{conversationId}/messages` | Load messages in a conversation.                      |
| `POST`   | `/api/Chat/conversations/{conversationId}/messages` | Send a message.                                       |
| `PUT`    | `/api/Chat/messages/{messageId}`                    | Edit own message.                                     |
| `DELETE` | `/api/Chat/messages/{messageId}`                    | Soft-delete own message.                              |
| `PUT`    | `/api/Chat/conversations/{conversationId}/read`     | Mark a conversation as read.                          |

### Start Conversation Request

The frontend sends numeric enum values for `type`.

```json
{
  "projectId": 1,
  "taskId": null,
  "title": "Site coordination",
  "type": 0,
  "participantUserIds": [2, 3]
}
```

Conversation type values:

| Value | Meaning            |
| ----- | ------------------ |
| `0`   | `PROJECT`          |
| `1`   | `TASK`             |
| `2`   | `MATERIAL_REQUEST` |
| `3`   | `PURCHASE_ORDER`   |

Backend requirements:

- Add the current authenticated user as a participant automatically, even if they are not included in `participantUserIds`.
- Allow `participantUserIds` to be empty so a user can start a conversation first and add people later, or return a clear validation message if the product decision is to require participants.
- Verify the user can access `projectId`.
- Return the created `ConversationResponse` immediately so the frontend can open the new thread.

### Conversation Response

```json
{
  "conversationId": 10,
  "projectId": 1,
  "taskId": null,
  "title": "Site coordination",
  "type": 0,
  "lastMessageAt": "2026-08-20T03:15:00Z",
  "participants": [
    {
      "userId": 1,
      "fullName": "Admin User",
      "email": "admin@example.com",
      "joinedAt": "2026-08-20T03:15:00Z",
      "lastReadAt": null
    }
  ]
}
```

The frontend accepts `type` as either a number or string, but request bodies are now numeric to avoid ASP.NET enum binding issues.

### Send Message Request

```json
{
  "body": "Can we confirm today's delivery?",
  "attachmentUrl": null
}
```

Return:

```json
{
  "messageId": 100,
  "conversationId": 10,
  "senderId": 1,
  "senderName": "Admin User",
  "body": "Can we confirm today's delivery?",
  "attachmentUrl": null,
  "sentAt": "2026-08-20T03:16:00Z",
  "editedAt": null,
  "deletedAt": null
}
```

## AI Chat

Base path: `/api/AiChat`

All endpoints require `Authorization: Bearer <accessToken>`.

### Required Endpoints

| Method   | Path                                        | Purpose                                               |
| -------- | ------------------------------------------- | ----------------------------------------------------- |
| `POST`   | `/api/AiChat/sessions`                      | Start an AI chat session.                             |
| `GET`    | `/api/AiChat/sessions`                      | List current user's AI chat sessions.                 |
| `GET`    | `/api/AiChat/sessions/{sessionId}/messages` | Load AI chat messages.                                |
| `POST`   | `/api/AiChat/sessions/{sessionId}/messages` | Send a user message and generate the assistant reply. |
| `DELETE` | `/api/AiChat/sessions/{sessionId}`          | Delete own AI session.                                |

### Start AI Session Request

```json
{
  "title": "Procurement advice",
  "projectId": 1
}
```

Both fields are optional. The frontend may send `{}` or omit the request body. The backend should scope the session to the current authenticated user.

### AI Session Response

Successful `POST /api/AiChat/sessions` response:

```json
{
  "statusCode": 200,
  "isSuccess": true,
  "errorMessage": null,
  "result": {
    "sessionId": 50,
    "userId": 1,
    "title": "Procurement advice",
    "projectId": 1,
    "createdAt": "2026-08-20T03:20:00Z",
    "lastMessageAt": null,
    "messageCount": 0
  }
}
```

If `projectId` is supplied but does not exist, return:

```json
{
  "statusCode": 404,
  "isSuccess": false,
  "errorMessage": "Project not found.",
  "result": null
}
```

If the AI chat tables are missing because the backend database has not applied migration `20260815191500_AddAiChatSessions`, return:

```json
{
  "statusCode": 503,
  "isSuccess": false,
  "errorMessage": "The database schema is not ready. Run the latest backend migrations.",
  "result": {
    "errorCode": "DATABASE_SCHEMA_NOT_READY",
    "retryable": false
  }
}
```

The frontend should treat `DATABASE_SCHEMA_NOT_READY` as an environment/backend setup problem, not as a user input problem.

### Send AI Message Request

```json
{
  "message": "Which supplier should we use for cement?",
  "useWebSearch": false
}
```

Preferred response:

```json
{
  "userMessage": {
    "messageId": 201,
    "sessionId": 50,
    "role": "user",
    "content": "Which supplier should we use for cement?",
    "useWebSearch": false,
    "createdAt": "2026-08-20T03:21:00Z"
  },
  "assistantMessage": {
    "messageId": 202,
    "sessionId": 50,
    "role": "assistant",
    "content": "Based on your catalog data...",
    "useWebSearch": false,
    "createdAt": "2026-08-20T03:21:02Z"
  },
  "webSearchUsed": false,
  "webSearchSummary": null
}
```

The current frontend also tolerates `sentAt` instead of `createdAt`, and a legacy single `message` field instead of `assistantMessage`.

## Common Failure Cases to Check

- `POST /api/Chat/conversations` returns 400 because `type` is sent as a string. The frontend now sends numeric enum values.
- `POST /api/Chat/conversations` creates a row but does not include the current user as a participant, causing the next message/list call to fail with 403 or return empty.
- `POST /api/AiChat/sessions` returns `503` with `DATABASE_SCHEMA_NOT_READY` because the running database has not applied migration `20260815191500_AddAiChatSessions`. Apply migrations before retrying.
- AI chat endpoints return a different DTO shape than the frontend expects. Use `createdAt` consistently, or keep `sentAt` while the frontend compatibility shim remains in place.
- Endpoints return raw objects instead of the standard envelope. The frontend can parse raw successful objects in some cases, but all app APIs should return the envelope for consistent errors.
