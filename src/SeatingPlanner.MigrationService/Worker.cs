using System.Diagnostics;
using SeatingPlanner.Shared.Persistence;

namespace SeatingPlanner.MigrationService;

/// <summary>
/// Applies EF Core migrations for every module, seeds sample data in Development, then exits.
/// The AppHost starts the Server only after this completes successfully.
/// </summary>
internal sealed class Worker(
    IServiceProvider serviceProvider,
    IHostEnvironment environment,
    IHostApplicationLifetime lifetime,
    ILogger<Worker> logger) : BackgroundService
{
    public const string ActivitySourceName = "SeatingPlanner.MigrationService";

    private static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var activity = ActivitySource.StartActivity("Migrating databases", ActivityKind.Client);

        try
        {
            await using var scope = serviceProvider.CreateAsyncScope();
            var initializers = scope.ServiceProvider.GetServices<IModuleDatabaseInitializer>();

            foreach (var initializer in initializers)
            {
                logger.LogInformation("Applying migrations for module {Module}", initializer.ModuleName);
                await initializer.MigrateAsync(stoppingToken);

                if (environment.IsDevelopment())
                {
                    logger.LogInformation("Seeding development data for module {Module}", initializer.ModuleName);
                    await initializer.SeedDevelopmentDataAsync(stoppingToken);
                }
            }
        }
        catch (Exception ex)
        {
            activity?.AddException(ex);
            logger.LogError(ex, "Database migration failed");

            // A non-zero exit code makes Aspire's WaitForCompletion keep the Server from starting.
            Environment.ExitCode = 1;
        }

        lifetime.StopApplication();
    }
}
