using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using SlimTrack.Data.Database;
using SlimTrack.Services;
using SlimTrack.Workers;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.AddNpgsqlDbContext<AppDbContext>("database");
builder.AddRabbitMQClient("messaging");

builder.Services.AddControllers().AddJsonOptions(options =>
{
    options.JsonSerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower;
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
});

builder.Services.AddOpenApi();

// Serviço de classificação heurística — substituível por NLP real no futuro
builder.Services.AddSingleton<IClassificationService, KeywordClassificationService>();

// Publisher de eventos para RabbitMQ
builder.Services.AddSingleton<IEventPublisher, RabbitMQEventPublisher>();

// Workers assíncronos
builder.Services.AddHostedService<OutboxPublisherWorker>();          // Outbox → RabbitMQ
builder.Services.AddHostedService<TicketClassificationWorker>();     // EmTriagem → Aberto (triagem automática)

// Autenticação JWT
var jwtKey = builder.Configuration["Jwt:Key"]
    ?? throw new InvalidOperationException("Jwt:Key não configurado no appsettings.json");

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"] ?? "SlimTrack",
            ValidAudience = builder.Configuration["Jwt:Audience"] ?? "SlimTrack",
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    logger.LogInformation("========================================");
    logger.LogInformation("VERIFICANDO CONEXÃO COM O BANCO DE DADOS");
    logger.LogInformation("========================================");

    var retries = 10;
    var delay = TimeSpan.FromSeconds(3);
    bool connected = false;

    for (var i = 0; i < retries; i++)
    {
        try
        {
            logger.LogInformation("Tentativa de conexão {Attempt}/{Total}...", i + 1, retries);

            var canConnect = await dbContext.Database.CanConnectAsync();
            if (canConnect)
            {
                logger.LogInformation("CONEXÃO COM O BANCO ESTABELECIDA!");
                connected = true;
                break;
            }
            else
            {
                throw new InvalidOperationException("CanConnectAsync retornou false");
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning("Tentativa {Attempt}/{Total} falhou: {Error}", i + 1, retries, ex.Message);

            if (i < retries - 1)
            {
                logger.LogInformation("Tentando novamente em {Delay}s...", delay.TotalSeconds);
                await Task.Delay(delay);
            }
        }
    }

    if (!connected)
    {
        logger.LogError("FALHA AO CONECTAR AO BANCO APÓS {Retries} TENTATIVAS", retries);
        throw new InvalidOperationException("Não foi possível conectar ao banco de dados");
    }

    // Aplicar migrations pendentes automaticamente
    try
    {
        logger.LogInformation("Verificando migrations pendentes...");

        var pendingMigrations = await dbContext.Database.GetPendingMigrationsAsync();
        var appliedMigrations = await dbContext.Database.GetAppliedMigrationsAsync();

        logger.LogInformation("Migrations aplicadas: {Count}", appliedMigrations.Count());
        logger.LogInformation("Migrations pendentes: {Count}", pendingMigrations.Count());

        if (pendingMigrations.Any())
        {
            logger.LogInformation("Aplicando {Count} migration(s) pendente(s)...", pendingMigrations.Count());
            await dbContext.Database.MigrateAsync();
            logger.LogInformation("Migrations aplicadas com sucesso!");
        }
        else
        {
            logger.LogInformation("Banco de dados atualizado. Nenhuma migration pendente.");
        }
    }
    catch (Exception ex)
    {
        logger.LogError(ex, "Falha ao aplicar migrations!");
        throw;
    }
}

app.MapDefaultEndpoints();

app.Use(async (context, next) =>
{
    try
    {
        await next();
    }
    catch (Exception ex)
    {
        var logger = context.RequestServices.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Exceção não tratada em {Method} {Path}", context.Request.Method, context.Request.Path);
        throw;
    }
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();

