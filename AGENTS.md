## Stack
- Backend: .NET 10 (LTS), orchestrated with .NET Aspire, build automation with Cake
- Frontend: React
- Database: PostgreSQL
- Cloud: AWS
- AI: Anthropic (Claude API)

## Architecture
- Modular monolith: each module owns its data and exposes a narrow public contract; no cross-module access to another module's internals or tables.
- Vertical slices: organise code by feature (request, handler, validation, persistence together), not by technical layer.

### Layout (`src/`)
- `SeatingPlanner.AppHost` — Aspire orchestration: postgres → `migrations` → `server` (`WaitForCompletion(migrations)`) → `frontend`.
- `SeatingPlanner.MigrationService` — worker that runs every `IModuleDatabaseInitializer` (migrate, plus seed in Development) then exits; non-zero exit blocks Server startup. Also the EF design-time startup project.
- `SeatingPlanner.Server` — composition root only: calls `builder.Add{Module}Module()` and `api.Map{Module}Endpoints()` under `/api`. No feature code here.
- `SeatingPlanner.Modules.{Name}` — one project per module (reference implementation: `Modules.Events`).
- `SeatingPlanner.Shared` — cross-cutting infrastructure only (`AddModuleDbContext`, `IModuleDatabaseInitializer`); no domain code.
- `frontend` — Vite + React + TS.

### Backend module conventions (see `Modules.Events`)
- `{Name}Module.cs` is the module's **only public type**: `Add{Name}Module(IHostApplicationBuilder)` + `Map{Name}Endpoints(IEndpointRouteBuilder)` (a `MapGroup("/{name}")` with `WithTags`). Everything else is `internal sealed`.
- Register the module in **both** `Server/Program.cs` and `MigrationService/Program.cs`, and add the project reference to both.
- `Domain/` — entities with private ctors, `private set`, a static `Create(...)` factory that guards invariants, and `const` length limits reused by EF config. IDs are `Guid.CreateVersion7()`, `ValueGeneratedNever()`.
- `Features/{FeatureName}/{FeatureName}.cs` — one static class per slice: nested request/response records, a `Map{FeatureName}` extension (`WithName`, `WithSummary`), and an `internal` `HandleAsync` (so tests can call it) returning `TypedResults`. Inject the module's `DbContext` directly; reads use `AsNoTracking()` and project to the response record.
- `Persistence/` — `{Name}DbContext` with `public const string Schema` and `HasDefaultSchema(Schema)`, registered via `builder.AddModuleDbContext<T>(Schema)` (own schema + own migrations history table). `{Name}DatabaseInitializer` implements `IModuleDatabaseInitializer`; dev seeding must be idempotent and use fixed GUIDs.
- Add a request for each new endpoint to `Server/SeatingPlanner.Server.http`.

### Backend unit tests (see `tests/SeatingPlanner.Modules.Events.Tests`)
- Setup for running tests (Docker, and the sandboxed PostgreSQL setup for Claude Code): `docs/running-tests.md`.
- One test project per module: `tests/SeatingPlanner.{Project}.Tests`, listed in `src/SeatingPlanner.slnx` under `/Tests/`. The module's csproj grants `<InternalsVisibleTo Include="SeatingPlanner.{Project}.Tests" />`.
- Stack: xUnit v3 (on Microsoft.Testing.Platform, opted in via the root `global.json`), Shouldly for assertions, Testcontainers.PostgreSql for the database. No mocking library, and no EF InMemory/SQLite: handlers run against real PostgreSQL (same image tag as the AppHost) so translation, collation and constraints match production.
- Layout mirrors the module: `Domain/{Entity}Tests.cs`, `Features/{FeatureName}Tests.cs`, shared helpers in `Infrastructure/`.
- Test slices by calling the feature's `HandleAsync` directly (declared `internal`, not `private`) and assert on the `TypedResults` type, status code and payload. HTTP-pipeline concerns (routing, binding) are out of scope for unit tests.
- Database-backed classes take `PostgresFixture` and are marked `[Collection(PostgresCollection.Name)]`. The fixture starts one Testcontainers container per run, or uses the server in `SEATINGPLANNER_TEST_POSTGRES` when set (it then drops the databases it created). Every test calls `CreateDatabaseAsync()` to get its own freshly migrated database, so tests never share data. Pure domain tests stay outside the collection and need no database.
- Use separate `DbContext` instances for Arrange (`database.SeedAsync`), Act (`database.CreateDbContext()`) and Assert (`database.GetAllEventsAsync()` or a new context), so the change tracker cannot mask what was persisted.
- AAA: every test has explicit `// Arrange`, `// Act`, `// Assert` comments and exactly one Act. Name tests `{Method}_{Scenario}_{ExpectedResult}`. Use `[Theory]`/`[InlineData]` for variants of one rule. Pass `TestContext.Current.CancellationToken` to async calls.
- Cover edge cases, not just the happy path: null/empty/whitespace input, exact length boundaries (min, max, max+1, values that become valid or invalid after trimming), case/Unicode variants, duplicates, no side effects on validation failure, cancellation, and `AsNoTracking` for reads.

### Frontend conventions
- Feature folders under `src/frontend/src/features/{feature}/`: `api.ts` (typed `fetch` wrappers taking an `AbortSignal`, throwing on `!response.ok`), `{Component}.tsx`, co-located `{Component}.css`. `App.tsx` only composes features.
- Call the backend with relative `/api/...` URLs (Vite proxies to the Server). Handle loading/error/empty states and abort in-flight fetches in `useEffect` cleanup.
- Reuse the global classes/tokens in `App.css`/`index.css` (`card`, `section-header`, `refresh-button`, `error-message`, `loading-skeleton`, `visually-hidden`, `--item-bg` etc.) before adding new ones.

## Dependencies
- Only permissive open-source licenses (MIT, Apache-2.0, BSD, ISC, PostgreSQL, 0BSD, BlueOak, CC0; MPL-2.0 is acceptable when unmodified). No commercial, dual-licensed/source-available, or copyleft (GPL/AGPL/LGPL/SSPL/BUSL) packages, including transitive ones.
- Known commercial-license traps to avoid: MediatR, AutoMapper, MassTransit (v9+), FluentAssertions (v8+), Duende IdentityServer, ImageSharp, QuestPDF (above revenue threshold), EPPlus (v5+), AG Grid Enterprise, MUI X Pro/Premium, Highcharts, Kendo/Telerik, DevExpress, Syncfusion.
- Check a package's license (and its latest version's license) before adding it; if unsure, ask.
- NuGet lock files are on for every project (`Directory.Build.props`). Commit each `packages.lock.json` with the package change; CI restores with `dotnet restore --locked-mode`. Pin container images used in code or tests by digest (`image:tag@sha256:...`).
- `node tools/check-licenses.mjs` checks every resolved NuGet and npm package (after restore/install) and runs as a Claude Code hook. Reviewed packages without an SPDX license id go in `tools/license-exceptions.json`, keyed by `name@version`.

## Commands
Standard `dotnet` (`build`, `test`, `run`, `format`, `ef`) and `npm` (`run build|lint`; `build` runs `tsc -b`, so it is also the typecheck) commands. Build automation goes through Cake.
- Run everything: `dotnet run --project src/SeatingPlanner.AppHost`.
- Run backend tests: `dotnet test --solution src/SeatingPlanner.slnx` (or `--project tests/<TestProject>`). Requires Docker for database-backed tests, unless `SEATINGPLANNER_TEST_POSTGRES` points at a test server.
- Add a migration: `dotnet ef migrations add <Name> --project src/SeatingPlanner.Modules.<Module> --startup-project src/SeatingPlanner.MigrationService --output-dir Persistence/Migrations`. Migrations are applied by the MigrationService at startup, not by `dotnet ef database update`.

## Environment variables
Default ASP.NET Core / .NET variables apply: `ASPNETCORE_ENVIRONMENT`, `DOTNET_ENVIRONMENT`, `ASPNETCORE_URLS`, and connection strings via `ConnectionStrings__<name>`. Never read or commit `.env*` or `appsettings.Production.json`; they are deny-listed.

## Git
- Never commit or push without explicit approval.
- Branches (no direct commits to `main` or `release/*`; use a branch and PR):
  - `main` — trunk
  - `feature/{short-description}` — new feature
  - `bugfix/{short-description}` — bug fix
  - `hotfix/{description}` — urgent fix
  - `release/{version}` — release branch
  - `chore/{description}` — non-code task, e.g. `chore/update-dependencies`

## Sandbox gotchas
- `.claude/settings.json` and `settings.local.json` are edit-denied; ask the user to change them.
- The `csharp-lsp` and `typescript-lsp` plugins need `csharp-ls` and `typescript-language-server` installed separately (setup and version pins: `docs/claude-code-lsp.md`). If LSP calls fail with a missing server, point the user there.
- `.sh` files and `.devcontainer/**` must keep LF line endings (see `.gitattributes`).
- The devcontainer firewall (`.devcontainer/init-firewall.sh`) is default-deny; new outbound domains must be added there and to `sandbox.network.allowedDomains` in `.claude/settings.json`.
- `dotnet ef database update`, `npm install`, `git commit`, `git push` and `git checkout` require approval; `dotnet ef database drop`, `dotnet user-secrets` and `git reset` are denied.
- On WSL the sandbox shell may resolve `npm`/`node` to the Windows install (`/mnt/c/Program Files/nodejs`), which cannot run inside the sandbox ("WSL 1 is not supported"). Load the Linux Node from nvm in the same command: `. ~/.nvm/nvm.sh && npm run build`.
- Inside the sandbox, run `dotnet build`/`test` with `-m:1`. On Linux, MSBuild worker nodes bind their pipes at a hard-coded `/tmp/MSBuild<pid>` (ignoring `TMPDIR`), and `/tmp` is read-only, so the workers crash and the build fails with "0 Error(s)" or hangs. Do not add `-m:1` to repo files.
- `dotnet test` and Testcontainers don't work in the sandbox (no Unix sockets, no host ports). Never exclude them from it; build with `-m:1` and run `tools/run-tests-sandboxed.sh` (setup: `docs/running-tests.md`).
- The sandbox masks `.gitmodules`, `.gitconfig`, `.bashrc` etc. in the repo root (they show as untracked; never commit them). SourceLink then fails reading `.gitmodules`; this is handled by `EnableSourceControlManagerQueries=false` in the `env` of `.claude/settings.local.json`, not in repo build files.
