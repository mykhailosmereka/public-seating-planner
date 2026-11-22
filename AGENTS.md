## Stack
- Backend: .NET 10 (LTS), orchestrated with .NET Aspire
- Frontend: React
- Database: PostgreSQL
- Planned, not yet in code: AWS (cloud) and Anthropic Claude API (AI). There are no SDKs or clients for them yet.

## Architecture
- Modular monolith: each module owns its data and exposes a narrow public contract; no cross-module access to another module's internals or tables.
- Vertical slices: organise code by feature, not by technical layer.

### Layout (`src/`)
- `SeatingPlanner.AppHost` — Aspire orchestration: postgres → `migrations` → `server` (`WaitForCompletion(migrations)`) → `webfrontend`.
- `SeatingPlanner.MigrationService` — worker that runs every `IModuleDatabaseInitializer` (migrate, plus seed in Development) then exits; non-zero exit blocks Server startup.
- `SeatingPlanner.Server` — composition root only: calls `builder.Add{Module}Module()` and `api.Map{Module}Endpoints()` under `/api`. No feature code here.
- `SeatingPlanner.Modules.{Name}` — one project per module (reference implementation: `Modules.Events`).
- `SeatingPlanner.Shared` — cross-cutting infrastructure only (`AddModuleDbContext`, `IModuleDatabaseInitializer`); no domain code.
- `frontend` (`src/frontend`, Aspire resource `webfrontend`) — Vite + React + TS.

### Tests
- Every new or changed feature slice and domain rule ships with unit tests in the same change; a feature is not done until its tests pass.

## Dependencies
- Only permissive open-source licenses (MIT, Apache-2.0, BSD, ISC, PostgreSQL, 0BSD, BlueOak, CC0; MPL-2.0 is acceptable when unmodified). No commercial, dual-licensed/source-available, or copyleft (GPL/AGPL/LGPL/SSPL/BUSL) packages, including transitive ones.
- Known commercial-license traps to avoid: MediatR, AutoMapper, MassTransit (v9+), FluentAssertions (v8+), Duende IdentityServer, ImageSharp, QuestPDF (above revenue threshold), EPPlus (v5+), AG Grid Enterprise, MUI X Pro/Premium, Highcharts, Kendo/Telerik, DevExpress, Syncfusion.
- Before adding a package, check its license (and its latest version's license); if unsure, ask. After adding or updating one, run `node tools/check-licenses.mjs`. Reviewed packages without an SPDX license id go in `tools/license-exceptions.json`, keyed by `name@version`.
- Commit each changed `packages.lock.json` with the package change. Pin container images used in code or tests by digest (`image:tag@sha256:...`).

## Commands
- Run everything: `dotnet run --project src/SeatingPlanner.AppHost`.
- Run backend tests: `dotnet test --solution src/SeatingPlanner.slnx` (or `--project tests/<TestProject>`). Needs Docker for database-backed tests, unless `SEATINGPLANNER_TEST_POSTGRES` points at a test server (setup: `docs/running-tests.md`).
- Frontend (`src/frontend`): `npm run build` (runs `tsc -b`, so it is also the typecheck) and `npm run lint`. There are no `test` or `typecheck` scripts.

## Git
- Never commit or push without explicit approval.
- No direct commits to `main` or `release/*`; use a branch and PR. Branch names: `feature/{short-description}`, `bugfix/{short-description}`, `hotfix/{description}`, `release/{version}`, `chore/{description}` (non-code task, e.g. `chore/update-dependencies`).
