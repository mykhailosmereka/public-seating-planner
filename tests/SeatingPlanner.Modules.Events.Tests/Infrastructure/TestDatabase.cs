using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql;
using SeatingPlanner.Modules.Events.Domain;
using SeatingPlanner.Modules.Events.Persistence;

namespace SeatingPlanner.Modules.Events.Tests.Infrastructure;

/// <summary>
/// A per-test PostgreSQL database with the Events migrations applied.
/// Use a separate <see cref="EventsDbContext"/> for Arrange, Act and Assert so the
/// change tracker never hides what was really persisted.
/// </summary>
internal sealed class TestDatabase(string serverConnectionString, string databaseName)
{
    private readonly string connectionString =
        new NpgsqlConnectionStringBuilder(serverConnectionString) { Database = databaseName }.ConnectionString;

    public EventsDbContext CreateDbContext()
    {
        // Mirrors SeatingPlanner.Shared.Persistence.ModuleDbContextExtensions.AddModuleDbContext.
        var options = new DbContextOptionsBuilder<EventsDbContext>()
            .UseNpgsql(connectionString, npgsql =>
                npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, EventsDbContext.Schema))
            .Options;

        return new EventsDbContext(options);
    }

    public async Task MigrateAsync()
    {
        await using var db = CreateDbContext();
        await db.Database.MigrateAsync();
    }

    public async Task SeedAsync(params Event[] events)
    {
        await using var db = CreateDbContext();
        db.Events.AddRange(events);
        await db.SaveChangesAsync();
    }

    public async Task<Event[]> GetAllEventsAsync()
    {
        await using var db = CreateDbContext();
        return await db.Events.AsNoTracking().ToArrayAsync();
    }
}
