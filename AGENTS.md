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
- `Features/{FeatureName}/{FeatureName}.cs` — one static class per slice: nested request/response records, a `Map{FeatureName}` extension (`WithName`, `WithSummary`), and a private `HandleAsync` returning `TypedResults`. Inject the module's `DbContext` directly; reads use `AsNoTracking()` and project to the response record.
- `Persistence/` — `{Name}DbContext` with `public const string Schema` and `HasDefaultSchema(Schema)`, registered via `builder.AddModuleDbContext<T>(Schema)` (own schema + own migrations history table). `{Name}DatabaseInitializer` implements `IModuleDatabaseInitializer`; dev seeding must be idempotent and use fixed GUIDs.
- Add a request for each new endpoint to `Server/SeatingPlanner.Server.http`.

### Frontend conventions
- Feature folders under `src/frontend/src/features/{feature}/`: `api.ts` (typed `fetch` wrappers taking an `AbortSignal`, throwing on `!response.ok`), `{Component}.tsx`, co-located `{Component}.css`. `App.tsx` only composes features.
- Call the backend with relative `/api/...` URLs (Vite proxies to the Server). Handle loading/error/empty states and abort in-flight fetches in `useEffect` cleanup.
- Reuse the global classes/tokens in `App.css`/`index.css` (`card`, `section-header`, `refresh-button`, `error-message`, `loading-skeleton`, `visually-hidden`, `--item-bg` etc.) before adding new ones.

## Commands
Standard `dotnet` (`build`, `test`, `run`, `format`, `ef`) and `npm` (`run build|lint`; `build` runs `tsc -b`, so it is also the typecheck) commands. Build automation goes through Cake.
- Run everything: `dotnet run --project src/SeatingPlanner.AppHost`.
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
- `.sh` files and `.devcontainer/**` must keep LF line endings (see `.gitattributes`).
- The devcontainer firewall (`.devcontainer/init-firewall.sh`) is default-deny; new outbound domains must be added there and to `sandbox.network.allowedDomains` in `.claude/settings.json`.
- `dotnet ef database update`, `npm install`, `git commit`, `git push` and `git checkout` require approval; `dotnet ef database drop`, `dotnet user-secrets` and `git reset` are denied.
- On WSL the sandbox shell may resolve `npm`/`node` to the Windows install (`/mnt/c/Program Files/nodejs`), which cannot run inside the sandbox ("WSL 1 is not supported"). Load the Linux Node from nvm in the same command: `. ~/.nvm/nvm.sh && npm run build`.
- Inside the sandbox, run `dotnet build`/`test` with `-m:1`. On Linux, MSBuild worker nodes bind their pipes at a hard-coded `/tmp/MSBuild<pid>` (ignoring `TMPDIR`), and `/tmp` is read-only, so the workers crash and the build fails with "0 Error(s)" or hangs. Do not add `-m:1` to repo files.
- The sandbox masks `.gitmodules`, `.gitconfig`, `.bashrc` etc. in the repo root (they show as untracked; never commit them). SourceLink then fails reading `.gitmodules`; this is handled by `EnableSourceControlManagerQueries=false` in the `env` of `.claude/settings.local.json`, not in repo build files.
