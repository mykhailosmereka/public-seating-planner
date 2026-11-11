using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql.EntityFrameworkCore.PostgreSQL.Infrastructure;

namespace SeatingPlanner.Shared.Persistence;

public static class ModuleDbContextExtensions
{
    /// <summary>
    /// Name of the PostgreSQL database resource declared in the Aspire AppHost.
    /// Aspire injects it as <c>ConnectionStrings__seatingplannerdb</c>.
    /// </summary>
    public const string ConnectionName = "seatingplannerdb";

    /// <summary>
    /// Registers a module-owned <see cref="DbContext"/> against the shared PostgreSQL database.
    /// Each module gets its own schema and its own migrations history table, so modules
    /// never share tables and can evolve their migrations independently.
    /// Aspire enrichment adds health checks, retries and OpenTelemetry tracing/metrics.
    /// </summary>
    /// <remarks>
    /// In the module's <see cref="DbContext.OnModelCreating"/>, call
    /// <c>modelBuilder.HasDefaultSchema(schema)</c> with the same schema name.
    /// </remarks>
    public static IHostApplicationBuilder AddModuleDbContext<TContext>(
        this IHostApplicationBuilder builder,
        string schema)
        where TContext : DbContext
    {
        var connectionString = builder.Configuration.GetConnectionString(ConnectionName);

        builder.Services.AddDbContextPool<TContext>(options =>
        {
            // The connection string is absent at design time (`dotnet ef migrations add`),
            // which is fine: EF only needs the provider to scaffold migrations.
            if (connectionString is null)
            {
                options.UseNpgsql(npgsql => ConfigureNpgsql(npgsql, schema));
            }
            else
            {
                options.UseNpgsql(connectionString, npgsql => ConfigureNpgsql(npgsql, schema));
            }
        });

        builder.EnrichNpgsqlDbContext<TContext>();

        return builder;
    }

    private static void ConfigureNpgsql(NpgsqlDbContextOptionsBuilder npgsql, string schema) =>
        npgsql.MigrationsHistoryTable(HistoryRepository.DefaultTableName, schema);
}
