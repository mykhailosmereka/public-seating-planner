using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using SeatingPlanner.Modules.Events.Persistence;

namespace SeatingPlanner.Modules.Events.Features.GetEvents;

/// <summary>GET /api/events — lists all events.</summary>
internal static class GetEvents
{
    public sealed record EventResponse(Guid Id, string Name);

    public static RouteHandlerBuilder MapGetEvents(this IEndpointRouteBuilder group) =>
        group.MapGet("/", HandleAsync)
            .WithName("GetEvents")
            .WithSummary("Lists all events.");

    private static async Task<Ok<EventResponse[]>> HandleAsync(
        EventsDbContext db,
        CancellationToken cancellationToken)
    {
        var events = await db.Events
            .AsNoTracking()
            .OrderBy(e => e.Name)
            .Select(e => new EventResponse(e.Id, e.Name))
            .ToArrayAsync(cancellationToken);

        return TypedResults.Ok(events);
    }
}
