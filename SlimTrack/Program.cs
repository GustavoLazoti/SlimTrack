using Microsoft.EntityFrameworkCore;
using SlimTrack.Data.Database;
using SlimTrack.Services;
using SlimTrack.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("database");
builder.AddRabbitMQClient("messaging");
builder.Services.AddControllers();
builder.Services.AddOpenApi();

builder.Services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();

builder.Services.AddHostedService<OrderEventConsumerWorker>();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    
    logger.LogInformation("========================================");
    logger.LogInformation("TESTING DATABASE CONNECTION");
    logger.LogInformation("========================================");
    
    var retries = 10;
    var delay = TimeSpan.FromSeconds(3);
    bool connected = false;
    
    for (var i = 0; i < retries; i++)
    {
        try
        {
            logger.LogInformation("Connection attempt {Attempt}/{Total}...", i + 1, retries);
            
            // Try to connect
            var canConnect = await dbContext.Database.CanConnectAsync();
            
            if (canConnect)
            {
                logger.LogInformation("✅ DATABASE CONNECTION SUCCESSFUL!");
                connected = true;
                
                // Check if tables exist
                try
                {
                    var ordersCount = await Microsoft.EntityFrameworkCore.EntityFrameworkQueryableExtensions.CountAsync(dbContext.Orders);
                    logger.LogInformation("Table 'Orders' exists with {Count} records", ordersCount);
                }
                catch (Exception ex)
                {
                    logger.LogWarning("Table 'Orders' does not exist yet: {Message}", ex.Message);
                }
                
                break;
            }
            else
            {
                throw new InvalidOperationException("CanConnectAsync returned false");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Connection attempt {Attempt}/{Total} failed: {Error}", 
                i + 1, retries, ex.Message);
            
            if (i < retries - 1)
            {
                logger.LogInformation("Retrying in {Delay} seconds...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
    }
    
    if (!connected)
    {
        logger.LogError("❌ FAILED TO CONNECT TO DATABASE AFTER {Retries} ATTEMPTS", retries);
        logger.LogError("Application will exit. Please check: PostgreSQL container is running (check Aspire dashboard)");
        throw new InvalidOperationException("Cannot connect to database");
    }
    
    logger.LogInformation("========================================");
    logger.LogInformation("READY TO APPLY MIGRATIONS (if needed)");
    logger.LogInformation("========================================");
    
    // STEP 2: Apply migrations
    try
    {
        logger.LogInformation("Checking for pending migrations...");
        
        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();
        
        logger.LogInformation("Applied migrations: {Count}", appliedMigrations.Count());
        foreach (var migration in appliedMigrations)
        {
            logger.LogInformation("Applied {Migration}", migration);
        }
        
        logger.LogInformation("Pending migrations: {Count}", pendingMigrations.Count());
        foreach (var migration in pendingMigrations)
        {
            logger.LogInformation("Pending {Migration}", migration);
        }
        
        if (pendingMigrations.Any())
        {
            logger.LogInformation("Applying {Count} pending migration(s)...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations applied successfully!");
        }
        else
        {
            logger.LogInformation("Database is up-to-date. No migrations needed.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "❌ Failed to apply migrations!");
        throw;
    }
}

app.MapDefaultEndpoints();

// Global middleware to log any unhandled exceptions so they appear in container logs / APM
app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Unhandled exception processing {Method} {Path}", context.Request.Method, context.Request.Path);
        throw;
    }
});

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
