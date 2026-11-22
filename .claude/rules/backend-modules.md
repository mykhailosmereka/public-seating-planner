---
paths:
  - "src/SeatingPlanner.Modules.*/**"
  - "src/SeatingPlanner.Server/**"
  - "src/SeatingPlanner.MigrationService/**"
---
# Backend module conventions (reference: `Modules.Events`)
- `{Name}Module.cs` is the module's **only public type**: `Add{Name}Module(IHostApplicationBuilder)` + `Map{Name}Endpoints(IEndpointRouteBuilder)` (a `MapGroup("/{name}")` with `WithTags`). Everything else is `internal sealed`.
- Register a module (and add the project reference) in every host that uses what the module provides. Endpoints go in the Server. An `IModuleDatabaseInitializer` (its own schema) goes in the MigrationService. Message handlers go in the worker that processes them. When you add a host, list it in the Layout section of `AGENTS.md` with what it hosts. Why: a host only needs the module's services for the work it actually does. Extra registrations add configuration requirements, and a missing one breaks quietly.
- `Domain/` — entities with private ctors, `private set`, a static `Create(...)` factory that guards invariants, and `const` length limits reused by EF config. IDs are `Guid.CreateVersion7()`, `ValueGeneratedNever()`.
- `Features/{FeatureName}/{FeatureName}.cs` — one static class per slice: nested request/response records, a `Map{FeatureName}` extension (`WithName`, `WithSummary`), and an `internal` `HandleAsync` (so tests can call it) returning `TypedResults`. Inject the module's `DbContext` directly; reads use `AsNoTracking()` and project to the response record.
- `Persistence/` — `{Name}DbContext` with `public const string Schema` and `HasDefaultSchema(Schema)`, registered via `builder.AddModuleDbContext<T>(Schema)` (own schema + own migrations history table). `{Name}DatabaseInitializer` implements `IModuleDatabaseInitializer`; dev seeding must be idempotent and use fixed GUIDs.
- Add a migration after changing a module's entities or `DbContext`: `dotnet ef migrations add <Name> --project src/SeatingPlanner.Modules.<Module> --startup-project src/SeatingPlanner.MigrationService --output-dir Persistence/Migrations`. Migrations are applied by the MigrationService at startup, not by `dotnet ef database update`.
- Add a request for each new endpoint to `Server/SeatingPlanner.Server.http`.
