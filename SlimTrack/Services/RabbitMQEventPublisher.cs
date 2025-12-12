using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace SlimTrack.Services;

public interface IEventPublisher
{
    Task PublishAsync<T>(string exchange, string routingKey, T @event) where T : class;
}

public class RabbitMQEventPublisher : IEventPublisher
{
    private readonly IConnection _connection;
    private readonly ILogger<RabbitMQEventPublisher> _logger;

    public RabbitMQEventPublisher(IConnection connection, ILogger<RabbitMQEventPublisher> logger)
    {
        _connection = connection;
        _logger = logger;
    }

    public async Task PublishAsync<T>(string exchange, string routingKey, T @event) where T : class
    {
        using var channel = await _connection.CreateChannelAsync();

        await channel.ExchangeDeclareAsync(
            exchange: exchange,
            type: ExchangeType.Topic,
            durable: true,
            autoDelete: false
        );

        var message = JsonSerializer.Serialize(@event);
        var body = Encoding.UTF8.GetBytes(message);

        var properties = new BasicProperties
        {
            Persistent = true,
            ContentType = "application/json",
            Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds())
        };

        await channel.BasicPublishAsync(
            exchange: exchange,
            routingKey: routingKey,
            mandatory: false,
            basicProperties: properties,
            body: body
        );

        _logger.LogInformation(
            "Published event {EventType} to exchange {Exchange} with routing key {RoutingKey}",
            typeof(T).Name,
            exchange,
            routingKey
        );
    }
}
