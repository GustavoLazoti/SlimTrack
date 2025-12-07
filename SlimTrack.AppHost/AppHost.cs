var builder = DistributedApplication.CreateBuilder(args);

var cache = builder.AddRedis("cache");
var database = builder.AddPostgres("database");

builder.AddProject<Projects.SlimTrack>("slimtrack")
        .WithReference(cache)
        .WithReference(database);

builder.Build().Run();
