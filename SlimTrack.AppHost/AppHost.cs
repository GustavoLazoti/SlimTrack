var builder = DistributedApplication.CreateBuilder(args);

// Redis with persistent volume
var cache = builder.AddRedis("cache")
    .WithDataVolume("slimtrack-redis-data")
    .WithLifetime(ContainerLifetime.Persistent);

// PostgreSQL with persistent volume
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("slimtrack-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent);

var database = postgres.AddDatabase("database");

builder.AddProject<Projects.SlimTrack>("slimtrack")
        .WithReference(cache)
        .WithReference(database)
        .WaitFor(database);

builder.Build().Run();
