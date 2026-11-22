---
paths:
  - "tests/**/*.cs"
  - "tests/**/*.csproj"
---
# Backend unit tests (reference: `tests/SeatingPlanner.Modules.Events.Tests`)
- Setup for running tests (Docker, and the sandboxed PostgreSQL setup for Claude Code): `docs/running-tests.md`.
- One test project per module: `tests/SeatingPlanner.{Project}.Tests`, listed in `src/SeatingPlanner.slnx` under `/Tests/`. The module's csproj grants `<InternalsVisibleTo Include="SeatingPlanner.{Project}.Tests" />`.
- Stack: xUnit v3 (on Microsoft.Testing.Platform, opted in via the root `global.json`), Shouldly for assertions, Testcontainers for infrastructure. No mocking library.
- Run real instances of the infrastructure we own and can start in a container (the database, and anything else the AppHost starts), using the same image as the AppHost. Mocks and EF InMemory/SQLite hide differences in query translation, collation and constraints. For services we don't control (Claude API, AWS, email), swap in a hand-written fake at the module's own interface. They cost money, need secrets and are non-deterministic, so tests must not call them.
- Layout mirrors the module: `Domain/{Entity}Tests.cs`, `Features/{FeatureName}Tests.cs`, shared helpers in `Infrastructure/`.
- Test slices by calling the feature's `HandleAsync` directly (declared `internal`, not `private`) and assert on the `TypedResults` type, status code and payload. HTTP-pipeline concerns (routing, binding) are out of scope for unit tests.
- Database-backed classes take the module's fixture (reference: `PostgresFixture` with `[Collection(PostgresCollection.Name)]`). The fixture starts one container per run, or uses the server in `SEATINGPLANNER_TEST_POSTGRES` when set (it then drops the databases it created). Every test gets its own freshly migrated database (`CreateDatabaseAsync()`), so tests never share data. Pure domain tests stay outside the collection and need no database.
- Use separate `DbContext` instances for Arrange, Act and Assert (assert through a fresh context or the module's `TestDatabase` helper), so the change tracker cannot mask what was persisted.
- AAA: every test has explicit `// Arrange`, `// Act`, `// Assert` comments and exactly one Act. Name tests `{Method}_{Scenario}_{ExpectedResult}`. Use `[Theory]`/`[InlineData]` for variants of one rule. Pass `TestContext.Current.CancellationToken` to async calls.
- Cover edge cases, not just the happy path: null/empty/whitespace input, exact length boundaries (min, max, max+1, values that become valid or invalid after trimming), case/Unicode variants, duplicates, no side effects on validation failure, cancellation, and `AsNoTracking` for reads.
