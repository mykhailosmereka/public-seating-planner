namespace SeatingPlanner.Shared.Persistence;

/// <summary>
/// Public contract a module exposes so the migration service can prepare its database
/// without reaching into the module's internals (DbContext, entities, migrations).
/// Register an implementation per module with <c>AddScoped&lt;IModuleDatabaseInitializer, ...&gt;</c>.
/// </summary>
public interface IModuleDatabaseInitializer
{
    /// <summary>Module name, used for logging.</summary>
    string ModuleName { get; }

    /// <summary>Applies all pending EF Core migrations for the module.</summary>
    Task MigrateAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Inserts sample data for local development. Must be idempotent: it runs on every start.
    /// Only invoked in the Development environment.
    /// </summary>
    Task SeedDevelopmentDataAsync(CancellationToken cancellationToken);
}
