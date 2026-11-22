using Microsoft.AspNetCore.Http;
using SeatingPlanner.Modules.Events.Domain;
using SeatingPlanner.Modules.Events.Features.GetEvents;
using SeatingPlanner.Modules.Events.Tests.Infrastructure;
using static SeatingPlanner.Modules.Events.Features.GetEvents.GetEvents;

namespace SeatingPlanner.Modules.Events.Tests.Features;

[Collection(PostgresCollection.Name)]
public sealed class GetEventsTests(PostgresFixture postgres)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_WithNoEvents_ReturnsOkWithEmptyArray()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();

        // Act
        var result = await GetEvents.HandleAsync(db, CancellationToken);

        // Assert
        result.StatusCode.ShouldBe(StatusCodes.Status200OK);
        result.Value.ShouldNotBeNull();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithSingleEvent_ReturnsItsIdAndName()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        var @event = Event.Create("Charity Gala");
        await database.SeedAsync(@event);
        await using var db = database.CreateDbContext();

        // Act
        var result = await GetEvents.HandleAsync(db, CancellationToken);

        // Assert
        result.Value!.ShouldHaveSingleItem().ShouldBe(new EventResponse(@event.Id, "Charity Gala"));
    }

    [Fact]
    public async Task HandleAsync_WithMultipleEvents_ReturnsAllOrderedByName()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(
            Event.Create("Tech Conference 2026"),
            Event.Create("Annual Company Dinner"),
            Event.Create("Graduation Ceremony"),
            Event.Create("Charity Gala"));
        await using var db = database.CreateDbContext();

        // Act
        var result = await GetEvents.HandleAsync(db, CancellationToken);

        // Assert
        result.Value!.Select(e => e.Name).ShouldBe(
        [
            "Annual Company Dinner",
            "Charity Gala",
            "Graduation Ceremony",
            "Tech Conference 2026",
        ]);
    }

    [Fact]
    public async Task HandleAsync_WithEventsWithSameName_ReturnsAllOfThem()
    {
        // Arrange: the database has no unique index on Name, so duplicates can exist.
        var database = await postgres.CreateDatabaseAsync();
        var first = Event.Create("Charity Gala");
        var second = Event.Create("Charity Gala");
        await database.SeedAsync(first, second);
        await using var db = database.CreateDbContext();

        // Act
        var result = await GetEvents.HandleAsync(db, CancellationToken);

        // Assert
        result.Value!.Select(e => e.Id).ShouldBe([first.Id, second.Id], ignoreOrder: true);
    }

    [Fact]
    public async Task HandleAsync_Always_DoesNotTrackEntities()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(Event.Create("Charity Gala"));
        await using var db = database.CreateDbContext();

        // Act
        await GetEvents.HandleAsync(db, CancellationToken);

        // Assert
        db.ChangeTracker.Entries().ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithCancelledToken_Throws()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => GetEvents.HandleAsync(db, cts.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
    }
}
