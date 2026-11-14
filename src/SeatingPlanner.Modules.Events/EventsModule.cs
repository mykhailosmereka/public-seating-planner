using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using SeatingPlanner.Modules.Events.Features.CreateEvent;
using SeatingPlanner.Modules.Events.Features.GetEvents;
using SeatingPlanner.Modules.Events.Persistence;
using SeatingPlanner.Shared.Persistence;

namespace SeatingPlanner.Modules.Events;

/// <summary>
/// Public contract of the Events module. Everything else in this assembly is internal.
/// </summary>
public static class EventsModule
{
    public static IHostApplicationBuilder AddEventsModule(this IHostApplicationBuilder builder)
    {
        builder.AddModuleDbContext<EventsDbContext>(EventsDbContext.Schema);
        builder.Services.AddScoped<IModuleDatabaseInitializer, EventsDatabaseInitializer>();

        return builder;
    }

    public static IEndpointRouteBuilder MapEventsEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/events").WithTags("Events");

        group.MapGetEvents();
        group.MapCreateEvent();

        return endpoints;
    }
}
