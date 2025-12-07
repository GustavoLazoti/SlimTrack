var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var postgres = builder.AddPostgres("postgres");
var database = postgres.AddDatabase("database");

builder.AddProject<Projects.SlimTrack>("slimtrack")
        .WithReference(cache)
        .WithReference(database);

builder.Build().Run();
