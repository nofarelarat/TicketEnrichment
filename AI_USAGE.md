# AI Usage

## Tools

- **Claude Code** (Claude Opus, in the VS Code extension) for the design discussion, the code,
  the tests and the first draft of these documents.
- A personal **`dotnet-developer` skill** loaded into Claude Code. It is a short instruction file
  holding my standing rules: understand before changing, propose a change and wait for approval
  before editing and prefered text convention.

## Workflow that worked

1. **Design before code.** defining E2E flow before any implementation.
2. **I challenged the design before accepting it.** 
3. **I made the product decisions.** I set the meaning of the identifier, replaced the proposed
   taxonomy with my own, and told it to build the mock first. The Claude provider was added
   afterwards, once the mock-backed service was working and tested.
4. **Small steps, each one verified.** Scaffold, domain, enrichment pipeline, API, tests and docs
   were separate commits, with a build or test run between them.

The most useful context was the constraint, not the feature list: "customers must never see an
error because of the AI provider" produced a better design than asking for endpoints would have.

## Where the AI got it wrong

- **Missing conflict/update flow** What will happen in case of same id with different content. Added as conflict response from POST function and added as gap to [DESIGN.md](DESIGN.md).

## How I checked AI-generated code

- **Tests first as the gate.** 60 tests run the real host against a real SQLite file, with a fake
  clock so retry and backoff are deterministic.
- **Running the real thing.** I started the service with the mock set to fail once and watched a
  ticket go `Pending`, fail an attempt, back off and complete. That is how the timestamp bug was
  found.
- **Asking "what does this accept that it should not?"** for the code that handles model output.
  The parser matches enum names itself because `Enum.TryParse` accepts `"1"` and `"Bug,Billing"`;
  both are test cases.
- **Reading the design before the diff.** Having agreed the design up front, review was a matter
  of checking the code against it, not reverse-engineering intent from generated code.
