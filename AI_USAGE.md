# AI Usage

## Tools

- **Claude Code** (Claude Opus, in the VS Code extension) for the design discussion, the code,
  the tests and the first draft of these documents.
- A personal **`dotnet-developer` skill** loaded into Claude Code. It is a short instruction file
  holding my standing rules: understand before changing, propose a change and wait for approval
  before editing, prefer plain classes over records, no `sealed`.

## Workflow that worked

1. **Design before code.** I gave the agent the assignment and asked it to review it and agree a
   design with me first. It proposed the API, the storage, the failure handling and the scope, and
   listed the decisions it needed from me. No file was written until I approved.
2. **I challenged the design before accepting it.** The proposal decoupled intake from enrichment.
   I asked how a client would then retrieve a ticket together with its enrichment. The answer
   (same host, same row, a status the client polls) is what made me accept it, and the trade-off
   it surfaced, that no client gets the enrichment in the submit response, became an open question
   in DESIGN.md.
3. **I made the product decisions.** I set the meaning of the identifier, replaced the proposed
   taxonomy with my own, and told it to build the mock first and leave the real provider as a
   described-only item.
4. **Small steps, each one verified.** Scaffold, domain, enrichment pipeline, API, tests and docs
   were separate commits, with a build or test run between them.

The most useful context was the constraint, not the feature list: "customers must never see an
error because of the AI provider" produced a better design than asking for endpoints would have.

## Where the AI got it wrong

- **Error responses had the wrong content type.** The generated controller carried
  `[Produces("application/json")]`. That attribute also overrides the content type of error
  responses, so problem details were served as `application/json` instead of
  `application/problem+json`. Seven API tests failed on the first run and pointed straight at it.
  The fix was removing the attribute. The failing tests and the fix are separate commits.
- **The submit response and later reads disagreed on `createdAt`.** Timestamps are stored at
  millisecond precision, but the `202` body returned the in-memory value with sub-millisecond
  digits. No test caught it, because the fake clock in the tests sat on a whole second. It showed
  up when running the real service and comparing the two JSON responses. The fix truncates the
  timestamp at intake, and there is now a regression test that advances the fake clock by a
  fraction of a millisecond first.
- **It defaulted to the new `.slnx` solution format.** That is the .NET 10 default, but I wanted
  the classic `.sln`. I corrected it and added the preference to my skill so it does not recur.

## How I checked AI-generated code

- **Tests first as the gate.** 57 tests run the real host against a real SQLite file, with a fake
  clock so retry and backoff are deterministic. Nothing was committed as done on a red run, apart
  from the one commit that deliberately records the failing tests before their fix.
- **Running the real thing.** I started the service with the mock set to fail once and watched a
  ticket go `Pending`, fail an attempt, back off and complete. That is how the timestamp bug was
  found.
- **Asking "what does this accept that it should not?"** for the code that handles model output.
  The parser matches enum names itself because `Enum.TryParse` accepts `"1"` and `"Bug,Billing"`;
  both are test cases.
- **Reading the design before the diff.** Having agreed the design up front, review was a matter
  of checking the code against it, not reverse-engineering intent from generated code.
