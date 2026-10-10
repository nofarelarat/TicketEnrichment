# Smart Support Ticket Service

A backend service that accepts customer support tickets, enriches each one with an AI-extracted
category, priority and one-sentence summary, and stays available when the AI provider does not.

It runs and passes its tests with no LLM credentials: the AI provider is a built-in mock that can
simulate the ways real providers fail.

## Requirements

- [.NET 10 SDK](https://dotnet.microsoft.com/download)

Nothing else. The data store is a local SQLite file created on first run.

## Run the service

```
dotnet run --project src/SupportTickets.Api
```

The service listens on `http://localhost:5180`.

## Run the tests

```
dotnet test
```

## Try it

Submit a ticket. The response is `202 Accepted` with the enrichment still `Pending`:

```
curl -i -X POST http://localhost:5180/api/v1/tickets \
  -H "Content-Type: application/json" \
  -d '{"id":"1001","subject":"Charged twice","message":"Urgent: I was charged twice, please refund."}'
```

Read it back a moment later. The enrichment is now `Completed`:

```
curl http://localhost:5180/api/v1/tickets/1001
```

```json
{
  "id": "1001",
  "subject": "Charged twice",
  "message": "Urgent: I was charged twice, please refund.",
  "createdAt": "2026-10-10T10:44:15.058+00:00",
  "enrichment": {
    "status": "Completed",
    "category": "Billing",
    "priority": "Urgent",
    "summary": "Customer reports: Charged twice.",
    "attempts": 1,
    "enrichedAt": "2026-10-10T10:44:15.542+00:00"
  }
}
```

| Endpoint | Purpose |
|---|---|
| `POST /api/v1/tickets` | Submit a ticket |
| `GET /api/v1/tickets/{id}` | Read a ticket with its enrichment |
| `GET /health` | Liveness |
| `GET /openapi/v1.json` | OpenAPI contract (a copy is in [docs/openapi.json](docs/openapi.json)) |

## See how it behaves when the AI provider fails

Start the service with a failure mode. Submissions still return `202`, reads still return `200`,
and the ticket stays `Pending` while the worker retries with backoff.

```
dotnet run --project src/SupportTickets.Api -- --Enrichment:Mock:FailureMode=Outage --Enrichment:Mock:FailuresBeforeSuccess=2
```

| `FailureMode` | Simulates |
|---|---|
| `None` (default) | A healthy provider |
| `Timeout` | A provider that never answers |
| `Outage` | HTTP 503 |
| `RateLimited` | HTTP 429 with a `Retry-After` |
| `MalformedJson` | Truncated, unparseable output |
| `OffSchema` | Valid JSON with values outside the schema |

`FailuresBeforeSuccess` is how many calls fail before the mock recovers. Leave it out and the mock
fails forever, so the ticket ends as `Failed` after `Enrichment:MaxAttempts` (default 5) and is
still stored and readable.

The same scenarios are covered by automated tests in
[ProviderFailureTests.cs](tests/SupportTickets.Tests/Enrichment/ProviderFailureTests.cs).

## More

- [DESIGN.md](DESIGN.md): API design, assumptions and open questions, trade-offs, what is not implemented.
- [AGENTS.md](AGENTS.md): rules for AI coding agents working in this repository.
- [AI_USAGE.md](AI_USAGE.md): how AI tools were used to build this.
