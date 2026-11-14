using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeatingPlanner.Modules.Events.Domain;
using SeatingPlanner.Modules.Events.Persistence;

namespace SeatingPlanner.Modules.Events.Features.CreateEvent;

/// <summary>POST /api/events — creates an event with a unique name.</summary>
internal static class CreateEvent
{
    public const int NameMinLength = 3;

    public sealed record CreateEventRequest(string? Name);

    public sealed record EventResponse(Guid Id, string Name);

    public static RouteHandlerBuilder MapCreateEvent(this IEndpointRouteBuilder group) =>
        group.MapPost("/", HandleAsync)
            .WithName("CreateEvent")
            .WithSummary("Creates an event. The name is trimmed and must be unique (case-insensitive).");

    private static async Task<Results<Created<EventResponse>, ValidationProblem>> HandleAsync(
        CreateEventRequest request,
        EventsDbContext db,
        CancellationToken cancellationToken)
    {
        var name = request.Name?.Trim() ?? string.Empty;

        if (name.Length is < NameMinLength or > Event.NameMaxLength)
        {
            return NameError(
                $"Event name must be between {NameMinLength} and {Event.NameMaxLength} characters.");
        }

        // No unique index on Name, so concurrent requests can still race; this check covers the normal case.
        var lowered = name.ToLowerInvariant();
        var exists = await db.Events
            .AnyAsync(e => e.Name.Trim().ToLower() == lowered, cancellationToken);

        if (exists)
        {
            return NameError($"An event named '{name}' already exists.");
        }

        var @event = Event.Create(name);
        db.Events.Add(@event);
        await db.SaveChangesAsync(cancellationToken);

        return TypedResults.Created((string?)null, new EventResponse(@event.Id, @event.Name));
    }

    private static ValidationProblem NameError(string message) =>
        TypedResults.ValidationProblem(new Dictionary<string, string[]>
        {
            ["name"] = [message],
        });
}
