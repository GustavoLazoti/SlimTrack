using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using SlimTrack.Data.Database;
using SlimTrack.Services;
using System.Text;

namespace SlimTrack.Workers;

/// <summary>
/// Worker responsible for publishing outbox messages to RabbitMQ.
/// This worker implements the Outbox Pattern to ensure reliable message delivery.
/// </summary>
public class OutboxPublisherWorker : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OutboxPublisherWorker> _logger;
    private readonly TimeSpan _interval = TimeSpan.FromSeconds(5);
    private const int MaxRetries = 5;

    public OutboxPublisherWorker(
        IServiceProvider serviceProvider,
        ILogger<OutboxPublisherWorker> logger)
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboxPublisherWorker starting...");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessPendingMessagesAsync(stoppingToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing outbox messages");
            }

            await Task.Delay(_interval, stoppingToken);
        }

        _logger.LogInformation("OutboxPublisherWorker stopping...");
    }

    private async Task ProcessPendingMessagesAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var connection = scope.ServiceProvider.GetRequiredService<IConnection>();

        // Searchar for pending outbox messages
        var pendingMessages = await dbContext.OutboxMessages
            .Where(m => !m.Published && m.RetryCount < MaxRetries)
            .OrderBy(m => m.CreatedAt)
            .Take(100) // Process up to 100 messages at a time
            .ToListAsync(cancellationToken);

        if (!pendingMessages.Any())
        {
            return;
        }

        _logger.LogInformation("Found {Count} pending outbox messages to publish", pendingMessages.Count);

        using var channel = await connection.CreateChannelAsync(cancellationToken: cancellationToken);

        // Declare exchange (idempotent operation)
        await channel.ExchangeDeclareAsync(
            exchange: "orders",
            type: RabbitMQ.Client.ExchangeType.Topic,
            durable: true,
            autoDelete: false,
            arguments: null,
            cancellationToken: cancellationToken
        );

        foreach (var message in pendingMessages)
        {
            try
            {
                var body = Encoding.UTF8.GetBytes(message.Payload);
                var properties = new BasicProperties
                {
                    Persistent = true,
                    ContentType = "application/json",
                    Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
                };

                await channel.BasicPublishAsync(
                    exchange: "orders",
                    routingKey: message.EventType,
                    mandatory: false,
                    basicProperties: properties,
                    body: body,
                    cancellationToken: cancellationToken
                );

                message.Published = true;
                message.PublishedAt = DateTime.UtcNow;
                message.ErrorMessage = null;

                _logger.LogInformation(
                    "Published outbox message {MessageId} of type {EventType}",
                    message.Id,
                    message.EventType
                );
            }
            catch (Exception ex)
            {
                message.RetryCount++;
                message.ErrorMessage = ex.Message;

                _logger.LogError(
                    ex,
                    "Failed to publish outbox message {MessageId} (retry {RetryCount}/{MaxRetries})",
                    message.Id,
                    message.RetryCount,
                    MaxRetries
                );
            }
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogInformation(
            "Processed {Total} outbox messages: {Published} published, {Failed} failed",
            pendingMessages.Count,
            pendingMessages.Count(m => m.Published),
            pendingMessages.Count(m => !m.Published)
        );
    }
}
