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

### Backend module conventions
- Path-scoped rules in `.claude/rules/backend-modules.md` (reference implementation: `Modules.Events`).

### Backend unit tests
- Path-scoped rules in `.claude/rules/backend-tests.md` (reference: `tests/SeatingPlanner.Modules.Events.Tests`). Setup for running tests: `docs/running-tests.md`.

### Frontend conventions
- Path-scoped rules in `.claude/rules/frontend.md`.

## Dependencies
- Only permissive open-source licenses (MIT, Apache-2.0, BSD, ISC, PostgreSQL, 0BSD, BlueOak, CC0; MPL-2.0 is acceptable when unmodified). No commercial, dual-licensed/source-available, or copyleft (GPL/AGPL/LGPL/SSPL/BUSL) packages, including transitive ones.
- Known commercial-license traps to avoid: MediatR, AutoMapper, MassTransit (v9+), FluentAssertions (v8+), Duende IdentityServer, ImageSharp, QuestPDF (above revenue threshold), EPPlus (v5+), AG Grid Enterprise, MUI X Pro/Premium, Highcharts, Kendo/Telerik, DevExpress, Syncfusion.
- Check a package's license (and its latest version's license) before adding it; if unsure, ask.
- NuGet lock files are on for every project (`Directory.Build.props`). Commit each `packages.lock.json` with the package change; CI restores with `dotnet restore --locked-mode`. Pin container images used in code or tests by digest (`image:tag@sha256:...`).
- `node tools/check-licenses.mjs` checks every resolved NuGet and npm package (after restore/install) and runs as a Claude Code hook. Reviewed packages without an SPDX license id go in `tools/license-exceptions.json`, keyed by `name@version`.

## Commands
Standard `dotnet` (`build`, `test`, `run`, `format`, `ef`) and `npm` (`run build|lint`; `build` runs `tsc -b`, so it is also the typecheck) commands.
- Run everything: `dotnet run --project src/SeatingPlanner.AppHost`.
- Run backend tests: `dotnet test --solution src/SeatingPlanner.slnx` (or `--project tests/<TestProject>`). Requires Docker for database-backed tests, unless `SEATINGPLANNER_TEST_POSTGRES` points at a test server.

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
- `.sh` files must keep LF line endings (see `.gitattributes`).
- New outbound domains must be added to `sandbox.network.allowedDomains` in `.claude/settings.json`.
- `dotnet ef database update`, `npm install`, `git commit`, `git push` and `git checkout` require approval; `dotnet ef database drop`, `dotnet user-secrets` and `git reset` are denied.
- On WSL the sandbox shell may resolve `npm`/`node` to the Windows install (`/mnt/c/Program Files/nodejs`), which cannot run inside the sandbox ("WSL 1 is not supported"). Load the Linux Node from nvm in the same command: `. ~/.nvm/nvm.sh && npm run build`.
- Inside the sandbox, run `dotnet build`/`test` with `-m:1`. On Linux, MSBuild worker nodes bind their pipes at a hard-coded `/tmp/MSBuild<pid>` (ignoring `TMPDIR`), and `/tmp` is read-only, so the workers crash and the build fails with "0 Error(s)" or hangs. Do not add `-m:1` to repo files.
- `dotnet test` and Testcontainers don't work in the sandbox (no Unix sockets, no host ports). Never exclude them from it; build with `-m:1` and run `tools/run-tests-sandboxed.sh` (setup: `docs/running-tests.md`).
- The sandbox masks `.gitmodules`, `.gitconfig`, `.bashrc` etc. in the repo root (they show as untracked; never commit them). SourceLink then fails reading `.gitmodules`; this is handled by `EnableSourceControlManagerQueries=false` in the `env` of `.claude/settings.local.json`, not in repo build files.
