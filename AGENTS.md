# AGENTS.md

Instructions for AI coding agents working in this repository. For the reasoning behind the
design, read [DESIGN.md](DESIGN.md).

## Commands

- Build: `dotnet build`
- Test: `dotnet test` (must pass before every commit; needs no credentials or network)
- Run: `dotnet run --project src/SupportTickets.Api`

## Layout

```
src/SupportTickets.Api/
  Api/          Controller and request/response contracts
  Tickets/      Intake: store and read tickets
  Enrichment/   Worker, processor, retry policy, output parser, provider seam
    Mock/       The mock AI provider and its failure modes (the default)
    Claude/     The Claude API provider (opt-in, needs ANTHROPIC_API_KEY)
  Domain/       Ticket and its state transitions, enums, limits
  Persistence/  EF Core DbContext (SQLite)
tests/SupportTickets.Tests/
```

## Invariants: do not break these

1. **The request path never calls the AI provider.** `POST` stores the ticket and returns. Only
   `EnrichmentProcessor` talks to `IEnrichmentProvider`.
2. **A stored ticket is never lost or deleted because enrichment failed.** Every attempt outcome
   is written to the ticket row.
3. **Provider errors never reach API clients.** Clients see `Pending`, `Completed` or `Failed`.
4. **Model output is untrusted.** It goes through `EnrichmentResultParser` before it is stored.
   Do not loosen the parser to make a provider "work"; fix the provider or the prompt.
5. **Ticket state changes only through methods on `Ticket`.** Do not add public setters.
6. **Tests need no real LLM and no secrets.** Never commit keys; read them from environment
   variables. Automated tests always run against the mock. The only code that calls the real
   Claude API on purpose is `--claude-smoke-test` in `Program.cs`, run by hand.
7. **Do not log ticket subject or message.** Log the ticket id.

## Conventions

- C# with nullable reference types on. Plain classes: no `record` types, no `sealed`.
- Constructor injection, `ILogger<T>`, the options pattern, `TimeProvider` for time (never
  `DateTime.UtcNow`), and a `CancellationToken` on every async method.
- Add an interface only where there is more than one implementation or a real seam.
- Errors from the API are problem details (`Problem(...)` in controllers).
- A new provider implements `IEnrichmentProvider`, returns the model's raw text, throws
  `EnrichmentProviderException` for outages and rate limits, and is
  registered in `EnrichmentServiceCollectionExtensions`.
- A new failure mode gets a `MockFailureMode` value and a case in `ProviderFailureTests`.

## Testing

- Integration tests use `TicketApiFactory`: the real host, a throwaway SQLite file, the worker
  off and a fake clock. Drive enrichment with `RunEnrichmentPassAsync()` and time with
  `Time.Advance(...)`. Do not add sleeps.
- Only `EnrichmentWorkerEndToEndTests` runs the real worker on the real clock. Keep it small.
- Every behaviour change comes with a test. A bug fix comes with a test that fails without it.

## Ask a human first

- Changing the API contract (then regenerate `docs/openapi.json` from `/openapi/v1.json`).
- Changing the category or priority taxonomy.
- Adding a NuGet package or changing the database schema.
- Anything that sends ticket content to a new external service.

## Commits

Small commits with a message that says why. Do not squash, and do not rewrite history.
