var builder = DistributedApplication.CreateBuilder(args);

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume()
    .WithLifetime(ContainerLifetime.Persistent)
    .WithPgAdmin(pgAdmin => pgAdmin.WithLifetime(ContainerLifetime.Persistent));

var database = postgres.AddDatabase("seatingplannerdb");

// Applies EF Core migrations for all modules (and seeds data in Development), then exits.
var migrations = builder.AddProject<Projects.SeatingPlanner_MigrationService>("migrations")
    .WithReference(database)
    .WaitFor(database);

var server = builder.AddProject<Projects.SeatingPlanner_Server>("server")
    .WithReference(database)
    .WaitForCompletion(migrations)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
