## Stack
- Backend: .NET 10 (LTS), orchestrated with .NET Aspire, build automation with Cake
- Frontend: React
- Database: PostgreSQL
- Cloud: AWS
- AI: Anthropic (Claude API)

## Architecture
- Modular monolith: each module owns its data and exposes a narrow public contract; no cross-module access to another module's internals or tables.
- Vertical slices: organise code by feature (request, handler, validation, persistence together), not by technical layer.

## Commands
Standard `dotnet` (`build`, `test`, `run`, `format`, `ef`) and `npm` (`run build|test|lint|typecheck`) commands. Build automation goes through Cake.

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
