using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SlimTrack.Data.Database;
using SlimTrack.Events;
using SlimTrack.Models;

namespace SlimTrack.Workers;

/// <summary>
/// Worker reponor processing order delivery completion.
/// </summary>
public class OrderCompletionWorker : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderCompletionWorker> _logger;
    private IChannel? _channel;

    private const string ExchangeName = "orders";
    private const string QueueName = "orders.delivered";
    private const string RoutingKey = "order.out_for_delivery";

    public OrderCompletionWorker(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<OrderCompletionWorker> logger)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderCompletionWorker starting...");

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
            _logger.LogInformation("OrderCompletionWorker started. Waiting for messages...");

            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderCompletionWorker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in OrderCompletionWorker");
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

            var order = await dbContext.Orders.AsNoTracking()
                .FirstOrDefaultAsync(o => o.Id == statusChangedEvent.OrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found. Rejecting...", statusChangedEvent.OrderId);
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            if (order.CurrentStatus != OrderStatus.OutForDelivery)
            {
                _logger.LogWarning("Order {OrderId} already processed (status: {Status}). ACKing...",
                    order.Id, order.CurrentStatus);
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, false, cancellationToken);
                return;
            }

            _logger.LogInformation("Completing delivery for order {OrderId}...", order.Id);
            await Task.Delay(6000, cancellationToken); // Here i simulate the delivery (could be anything, like an BTG integration or an integration with the log company)

            var rowsAffected = await dbContext.Database.ExecuteSqlAsync(
                $@"UPDATE ""Orders"" 
                   SET ""CurrentStatus"" = {(int)OrderStatus.Delivered}, 
                       ""UpdatedAt"" = {DateTime.UtcNow}
                   WHERE ""Id"" = {order.Id} 
                   AND ""CurrentStatus"" = {(int)OrderStatus.OutForDelivery}",
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
                Status = OrderStatus.Delivered,
                Message = "Pedido entregue com sucesso",
                Timestamp = DateTime.UtcNow
            };
            dbContext.OrderEvents.Add(newEvent);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("? Order {OrderId} DELIVERED successfully!", order.Id);

            // ? Publicar evento de entrega concluída (para notificações, analytics, etc)
            var eventPublisher = scope.ServiceProvider.GetRequiredService<Services.IEventPublisher>();
            var deliveredEvent = new OrderStatusChangedEvent
            {
                OrderId = order.Id,
                OldStatus = OrderStatus.OutForDelivery,
                NewStatus = OrderStatus.Delivered,
                Message = "Pedido entregue com sucesso",
                ChangedAt = DateTime.UtcNow
            };
            await eventPublisher.PublishAsync(ExchangeName, "order.delivered", deliveredEvent);
            _logger.LogInformation("Published order.delivered event for order {OrderId}", order.Id);

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
        _logger.LogInformation("OrderCompletionWorker stopping...");
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}
