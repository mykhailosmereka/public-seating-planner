using System.Collections.Concurrent;
using Npgsql;
using Testcontainers.PostgreSql;

namespace SeatingPlanner.Modules.Events.Tests.Infrastructure;

/// <summary>
/// One PostgreSQL server shared by every test class in <see cref="PostgresCollection"/>.
/// Each test gets its own database from <see cref="CreateDatabaseAsync"/>, so tests never see each other's data.
/// </summary>
/// <remarks>
/// By default the server is a Testcontainers container (needs Docker). When
/// <see cref="ConnectionStringVariable"/> is set, that existing server is used instead and every
/// database created by the run is dropped afterwards. The user must be allowed to create databases.
/// </remarks>
public sealed class PostgresFixture : IAsyncLifetime
{
    public const string ConnectionStringVariable = "SEATINGPLANNER_TEST_POSTGRES";

    // Same version as the Aspire AppHost (Aspire.Hosting.PostgreSQL 13.6.4), pinned by digest so a re-pushed tag cannot change it.
    private const string Image = "postgres:18.3@sha256:7e32e9833a6fb1c92c32552794cb6ed569d51b445a54907d35fc112ef39684db";

    private readonly ConcurrentBag<string> createdDatabases = [];
    private PostgreSqlContainer? container;
    private string serverConnectionString = null!;

    public async ValueTask InitializeAsync()
    {
        var external = Environment.GetEnvironmentVariable(ConnectionStringVariable);

        if (!string.IsNullOrWhiteSpace(external))
        {
            EnsureLocalTestServer(external);
            serverConnectionString = external;
            return;
        }

        container = new PostgreSqlBuilder(Image).Build();
        await container.StartAsync();
        serverConnectionString = container.GetConnectionString();
    }

    public async ValueTask DisposeAsync()
    {
        if (container is not null)
        {
            // The databases disappear with the container.
            await container.DisposeAsync();
            return;
        }

        NpgsqlConnection.ClearAllPools();

        await using var connection = new NpgsqlConnection(serverConnectionString);
        await connection.OpenAsync();

        foreach (var database in createdDatabases)
        {
            await using var command = new NpgsqlCommand($"DROP DATABASE IF EXISTS \"{database}\" WITH (FORCE)", connection);
            await command.ExecuteNonQueryAsync();
        }
    }

    /// <summary>
    /// The fixture creates and force-drops databases, so refuse anything that does not look like a
    /// dedicated local test server (loopback host and a test-only user).
    /// </summary>
    private static void EnsureLocalTestServer(string connectionString)
    {
        var target = new NpgsqlConnectionStringBuilder(connectionString);
        var isLoopback = target.Host is "localhost" or "127.0.0.1" or "::1";
        var isTestUser = target.Username?.Contains("test", StringComparison.OrdinalIgnoreCase) == true;

        if (!isLoopback || !isTestUser)
        {
            throw new InvalidOperationException(
                $"{ConnectionStringVariable} must point to a local test server (localhost) with a test-only user " +
                $"(user name containing 'test'); got Host={target.Host}, Username={target.Username}.");
        }
    }

    /// <summary>Creates a fresh, fully migrated database for a single test.</summary>
    internal async Task<TestDatabase> CreateDatabaseAsync()
    {
        var name = $"events_test_{Guid.NewGuid():N}";
        createdDatabases.Add(name);

        var database = new TestDatabase(serverConnectionString, name);
        await database.MigrateAsync();
        return database;
    }
}

/// <summary>
/// Put database-backed test classes in this collection: <c>[Collection(PostgresCollection.Name)]</c>.
/// Pure unit tests (e.g. domain) stay outside it, so they run without a database.
/// </summary>
[CollectionDefinition(Name)]
public sealed class PostgresCollection : ICollectionFixture<PostgresFixture>
{
    public const string Name = "Postgres";
}
