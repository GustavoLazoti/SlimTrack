var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache")
    .WithDataVolume("slimtrack-redis-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("slimtrack-redis");

var postgres = builder.AddPostgres("postgres")
    .WithDataVolume("slimtrack-postgres-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("slimtrack-postgres");

var database = postgres.AddDatabase("database");

var rabbitmq = builder.AddRabbitMQ("messaging")
    .WithDataVolume("slimtrack-rabbitmq-data")
    .WithLifetime(ContainerLifetime.Persistent)
    .WithContainerName("slimtrack-rabbitmq")
    .WithManagementPlugin();

builder.AddProject<Projects.SlimTrack>("slimtrack")
        .WithReference(cache)
        .WithReference(database)
        .WithReference(rabbitmq)
        .WaitFor(database)
        .WaitFor(rabbitmq);

builder.Build().Run();
