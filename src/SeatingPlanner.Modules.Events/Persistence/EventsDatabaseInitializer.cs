using Microsoft.EntityFrameworkCore;
using SeatingPlanner.Modules.Events.Domain;
using SeatingPlanner.Shared.Persistence;

namespace SeatingPlanner.Modules.Events.Persistence;

internal sealed class EventsDatabaseInitializer(EventsDbContext db) : IModuleDatabaseInitializer
{
    // Fixed UUID v7 IDs so local URLs and .http requests stay stable across database resets.
    private static readonly (Guid Id, string Name)[] SeedEvents =
    [
        (Guid.Parse("01a1ca7a-ae00-7b0f-9c06-2cc1ea1f1255"), "Annual Company Dinner"),
        (Guid.Parse("01a1ca7a-ae01-7e45-b80f-eea672ad52f0"), "Smith & Jones Wedding"),
        (Guid.Parse("01a1ca7a-ae02-7f97-9a5c-9696a0710383"), "Tech Conference 2026"),
        (Guid.Parse("01a1ca7a-ae03-72b1-9141-826c4dc0602c"), "Charity Gala"),
        (Guid.Parse("01a1ca7a-ae04-7a6a-aa79-1c5081258520"), "Graduation Ceremony"),
    ];

    public string ModuleName => "Events";

    public Task MigrateAsync(CancellationToken cancellationToken) =>
        db.Database.MigrateAsync(cancellationToken);

    /// <summary>Inserts any seed events that are missing; existing rows are left untouched.</summary>
    public async Task SeedDevelopmentDataAsync(CancellationToken cancellationToken)
    {
        var seedIds = SeedEvents.Select(e => e.Id).ToArray();

        var existingIds = await db.Events
            .Where(e => seedIds.Contains(e.Id))
            .Select(e => e.Id)
            .ToListAsync(cancellationToken);

        var missing = SeedEvents
            .Where(e => !existingIds.Contains(e.Id))
            .Select(e => Event.Create(e.Id, e.Name))
            .ToList();

        if (missing.Count == 0)
        {
            return;
        }

        db.Events.AddRange(missing);
        await db.SaveChangesAsync(cancellationToken);
    }
}
