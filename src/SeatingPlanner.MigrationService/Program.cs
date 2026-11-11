using SeatingPlanner.MigrationService;
using SeatingPlanner.Modules.Events;

var builder = Host.CreateApplicationBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenTelemetry()
    .WithTracing(tracing => tracing.AddSource(Worker.ActivitySourceName));

// Register every module that owns a database schema.
builder.AddEventsModule();

builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
