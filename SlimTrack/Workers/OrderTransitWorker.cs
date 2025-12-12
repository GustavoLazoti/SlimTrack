using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SlimTrack.Data.Database;
using SlimTrack.Events;
using SlimTrack.Models;
using SlimTrack.Services;

namespace SlimTrack.Workers;

/// <summary>
/// Worker responsible for processing order transit stage.
/// </summary>
public class OrderTransitWorker : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderTransitWorker> _logger;
    private IChannel? _channel;

    private const string ExchangeName = "orders";
    private const string QueueName = "orders.transit";
    private const string RoutingKey = "order.processing";

    public OrderTransitWorker(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<OrderTransitWorker> logger)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderTransitWorker starting...");

        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);
            await _channel.BasicQosAsync(0, 1, false, stoppingToken);

            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);
            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken);
            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                arguments: null,
                cancellationToken: stoppingToken);

            _logger.LogInformation("Queue '{Queue}' bound to exchange '{Exchange}' with routing key '{RoutingKey}'",
                QueueName, ExchangeName, RoutingKey);

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, eventArgs) => await ProcessMessageAsync(eventArgs, stoppingToken);

            await _channel.BasicConsumeAsync(QueueName, false, consumer, stoppingToken);
            _logger.LogInformation("OrderTransitWorker started. Waiting for messages...");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderTransitWorker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in OrderTransitWorker");
            throw;
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        var messageBody = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        _logger.LogInformation("Received message: {Message}", messageBody);

        try
        {
            var statusChangedEvent = JsonSerializer.Deserialize<OrderStatusChangedEvent>(messageBody);
            if (statusChangedEvent == null)
            {
                _logger.LogWarning("Failed to deserialize. Rejecting...");
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var order = await dbContext.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == statusChangedEvent.OrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found. Rejecting...", statusChangedEvent.OrderId);
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            if (order.CurrentStatus != OrderStatus.Processing)
            {
                _logger.LogWarning("Order {OrderId} already processed (status: {Status}). ACKing...",
                    order.Id, order.CurrentStatus);
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            _logger.LogInformation("Moving order {OrderId} to transit...", order.Id);
            await Task.Delay(5000, cancellationToken); // Simulates processing time

            var rowsAffected = await dbContext.Database.ExecuteSqlAsync(
                $@"UPDATE ""Orders"" 
                   SET ""CurrentStatus"" = {(int)OrderStatus.InTransit}, 
                       ""UpdatedAt"" = {DateTime.UtcNow}
                   WHERE ""Id"" = {order.Id} 
                   AND ""CurrentStatus"" = {(int)OrderStatus.Processing}",
                cancellationToken);

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Order {OrderId} already updated. ACKing...", order.Id);
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            var newEvent = new OrderEvent
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = OrderStatus.InTransit,
                Message = "Pedido em trânsito/transporte",
                Timestamp = DateTime.UtcNow
            };
            dbContext.OrderEvents.Add(newEvent);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Order {OrderId} moved to InTransit", order.Id);
            
            var nextEvent = new OrderStatusChangedEvent
            {
                OrderId = order.Id,
                OldStatus = OrderStatus.Processing,
                NewStatus = OrderStatus.InTransit,
                Message = "Pedido em trânsito",
                ChangedAt = DateTime.UtcNow
            };
            await eventPublisher.PublishAsync(ExchangeName, "order.in_transit", nextEvent);

            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing cancelled. Requeuing...");
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, false, true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing. Requeuing...");
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, false, true, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderTransitWorker stopping...");
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}
