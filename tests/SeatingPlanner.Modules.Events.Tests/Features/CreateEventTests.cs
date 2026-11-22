using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using SeatingPlanner.Modules.Events.Domain;
using SeatingPlanner.Modules.Events.Features.CreateEvent;
using SeatingPlanner.Modules.Events.Tests.Infrastructure;
using static SeatingPlanner.Modules.Events.Features.CreateEvent.CreateEvent;

namespace SeatingPlanner.Modules.Events.Tests.Features;

[Collection(PostgresCollection.Name)]
public sealed class CreateEventTests(PostgresFixture postgres)
{
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HandleAsync_WithValidName_ReturnsCreatedEvent()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest("Charity Gala");

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        var created = result.Result.ShouldBeOfType<Created<EventResponse>>();
        created.StatusCode.ShouldBe(StatusCodes.Status201Created);
        created.Location.ShouldBeNull();
        created.Value.ShouldNotBeNull();
        created.Value.Name.ShouldBe("Charity Gala");
        created.Value.Id.Version.ShouldBe(7);
    }

    [Fact]
    public async Task HandleAsync_WithValidName_PersistsEvent()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest("Charity Gala");

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        var response = result.Result.ShouldBeOfType<Created<EventResponse>>().Value!;
        var stored = (await database.GetAllEventsAsync()).ShouldHaveSingleItem();
        stored.Id.ShouldBe(response.Id);
        stored.Name.ShouldBe("Charity Gala");
    }

    [Theory]
    [InlineData("  Charity Gala  ")]
    [InlineData("\tCharity Gala\r\n")]
    public async Task HandleAsync_WithSurroundingWhitespace_TrimsName(string name)
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(name);

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        result.Result.ShouldBeOfType<Created<EventResponse>>().Value!.Name.ShouldBe("Charity Gala");
        (await database.GetAllEventsAsync()).ShouldHaveSingleItem().Name.ShouldBe("Charity Gala");
    }

    [Theory]
    [InlineData(CreateEvent.NameMinLength)]
    [InlineData(Event.NameMaxLength)]
    public async Task HandleAsync_WithNameAtLengthBoundary_ReturnsCreated(int length)
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(new string('a', length));

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        result.Result.ShouldBeOfType<Created<EventResponse>>().Value!.Name.Length.ShouldBe(length);
    }

    [Fact]
    public async Task HandleAsync_WithMaxLengthNamePaddedByWhitespace_ReturnsCreated()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var name = new string('a', Event.NameMaxLength);
        var request = new CreateEventRequest($"   {name}   ");

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        result.Result.ShouldBeOfType<Created<EventResponse>>().Value!.Name.ShouldBe(name);
    }

    [Fact]
    public async Task HandleAsync_WithUnicodeName_PersistsNameUnchanged()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        const string name = "Événement d'été 🎉";
        var request = new CreateEventRequest(name);

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        result.Result.ShouldBeOfType<Created<EventResponse>>();
        (await database.GetAllEventsAsync()).ShouldHaveSingleItem().Name.ShouldBe(name);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ab")]
    [InlineData("  ab  ")]
    public async Task HandleAsync_WithNameTooShort_ReturnsValidationProblem(string? name)
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(name);

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        ShouldBeNameError(result, "Event name must be between 3 and 100 characters.");
        (await database.GetAllEventsAsync()).ShouldBeEmpty();
    }

    [Fact]
    public async Task HandleAsync_WithNameOverMaxLength_ReturnsValidationProblem()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(new string('a', Event.NameMaxLength + 1));

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        ShouldBeNameError(result, "Event name must be between 3 and 100 characters.");
        (await database.GetAllEventsAsync()).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("Charity Gala")]
    [InlineData("charity gala")]
    [InlineData("CHARITY GALA")]
    [InlineData("  Charity Gala  ")]
    public async Task HandleAsync_WithDuplicateName_ReturnsValidationProblem(string name)
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(Event.Create("Charity Gala"));
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(name);

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        ShouldBeNameError(result, $"An event named '{name.Trim()}' already exists.");
        (await database.GetAllEventsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateOfUntrimmedStoredName_ReturnsValidationProblem()
    {
        // Arrange: rows written outside this endpoint (e.g. seeding) may not be trimmed.
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(Event.Create("  Charity Gala  "));
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest("Charity Gala");

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        ShouldBeNameError(result, "An event named 'Charity Gala' already exists.");
    }

    [Fact]
    public async Task HandleAsync_WithDuplicateNonAsciiNameInDifferentCase_ReturnsValidationProblem()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(Event.Create("ÉVÉNEMENT D'ÉTÉ"));
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest("événement d'été");

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        ShouldBeNameError(result, "An event named 'événement d'été' already exists.");
    }

    [Theory]
    [InlineData("Charity Gala 2026")]
    [InlineData("Charity")]
    [InlineData("Charity  Gala")]
    public async Task HandleAsync_WithSimilarButDifferentName_ReturnsCreated(string name)
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await database.SeedAsync(Event.Create("Charity Gala"));
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest(name);

        // Act
        var result = await CreateEvent.HandleAsync(request, db, CancellationToken);

        // Assert
        result.Result.ShouldBeOfType<Created<EventResponse>>();
        (await database.GetAllEventsAsync()).Length.ShouldBe(2);
    }

    [Fact]
    public async Task HandleAsync_CalledTwiceWithSameName_CreatesOnlyFirst()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var firstDb = database.CreateDbContext();
        await using var secondDb = database.CreateDbContext();
        var request = new CreateEventRequest("Charity Gala");

        // Act
        var first = await CreateEvent.HandleAsync(request, firstDb, CancellationToken);
        var second = await CreateEvent.HandleAsync(request, secondDb, CancellationToken);

        // Assert
        first.Result.ShouldBeOfType<Created<EventResponse>>();
        second.Result.ShouldBeOfType<ValidationProblem>();
        (await database.GetAllEventsAsync()).ShouldHaveSingleItem();
    }

    [Fact]
    public async Task HandleAsync_WithCancelledToken_ThrowsAndPersistsNothing()
    {
        // Arrange
        var database = await postgres.CreateDatabaseAsync();
        await using var db = database.CreateDbContext();
        var request = new CreateEventRequest("Charity Gala");
        using var cts = new CancellationTokenSource();
        await cts.CancelAsync();

        // Act
        var exception = await Record.ExceptionAsync(() => CreateEvent.HandleAsync(request, db, cts.Token));

        // Assert
        exception.ShouldBeAssignableTo<OperationCanceledException>();
        (await database.GetAllEventsAsync()).ShouldBeEmpty();
    }

    private static void ShouldBeNameError(
        Results<Created<EventResponse>, ValidationProblem> result,
        string expectedMessage)
    {
        var problem = result.Result.ShouldBeOfType<ValidationProblem>();
        problem.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        problem.ProblemDetails.Errors.ShouldHaveSingleItem().Key.ShouldBe("name");
        problem.ProblemDetails.Errors["name"].ShouldBe([expectedMessage]);
    }
}
