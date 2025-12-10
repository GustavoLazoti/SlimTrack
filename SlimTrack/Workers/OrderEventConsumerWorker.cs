using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using SlimTrack.Data.Database;
using SlimTrack.Events;
using SlimTrack.Models;

namespace SlimTrack.Workers;

public class OrderEventConsumerWorker : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<OrderEventConsumerWorker> _logger;
    private IChannel? _channel;

    private const string ExchangeName = "orders";
    private const string QueueName = "orders.processing";
    private const string RoutingKey = "order.created";

    public OrderEventConsumerWorker(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<OrderEventConsumerWorker> logger)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OrderEventConsumerWorker starting...");

        try
        {
            // Criar canal
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Configurar QoS - processar uma mensagem por vez
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: stoppingToken
            );

            // Declarar exchange (idempotente)
            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // Declarar fila (idempotente)
            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,        // ? Persiste no disco
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            // Fazer binding (vincular fila ao exchange)
            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                arguments: null,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation(
                "Queue '{Queue}' bound to exchange '{Exchange}' with routing key '{RoutingKey}'",
                QueueName,
                ExchangeName,
                RoutingKey
            );

            // Criar consumer
            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (sender, eventArgs) =>
            {
                await ProcessMessageAsync(eventArgs, stoppingToken);
            };

            // Iniciar consumo
            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,  // ? Manual ACK (confiabilidade)
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("OrderEventConsumerWorker started successfully. Waiting for messages...");

            // Manter worker rodando
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("OrderEventConsumerWorker stopping...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fatal error in OrderEventConsumerWorker");
            throw;
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        var messageBody = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        
        _logger.LogInformation(
            "Received message from queue '{Queue}': {Message}",
            QueueName,
            messageBody
        );

        try
        {
            var orderCreatedEvent = JsonSerializer.Deserialize<OrderCreatedEvent>(messageBody);
            
            if (orderCreatedEvent == null)
            {
                _logger.LogWarning("Failed to deserialize message. Rejecting...");
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            var order = await dbContext.Orders
                .AsNoTracking()
                .Include(o => o.Events)
                .FirstOrDefaultAsync(o => o.Id == orderCreatedEvent.OrderId, cancellationToken);

            if (order == null)
            {
                _logger.LogWarning("Order {OrderId} not found. Rejecting message...", orderCreatedEvent.OrderId);
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            if (order.CurrentStatus != OrderStatus.Received)
            {
                _logger.LogWarning(
                    "Order {OrderId} already processed (status: {Status}). Skipping and ACKing...",
                    order.Id,
                    order.CurrentStatus
                );
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            _logger.LogInformation("Processing order {OrderId}...", order.Id);
            await Task.Delay(3000, cancellationToken);

            var rowsAffected = await dbContext.Database.ExecuteSqlAsync(
                $@"UPDATE ""Orders"" 
                   SET ""CurrentStatus"" = {(int)OrderStatus.Processing}, 
                       ""UpdatedAt"" = {DateTime.UtcNow}
                   WHERE ""Id"" = {order.Id} 
                   AND ""CurrentStatus"" = {(int)OrderStatus.Received}",
                cancellationToken
            );

            if (rowsAffected == 0)
            {
                _logger.LogWarning(
                    "Order {OrderId} was already updated by another process. Skipping and ACKing...",
                    order.Id
                );
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            var newEvent = new OrderEvent
            {
                Id = Guid.NewGuid(),
                OrderId = order.Id,
                Status = OrderStatus.Processing,
                Message = "Pedido em processamento/separação",
                Timestamp = DateTime.UtcNow
            };

            dbContext.OrderEvents.Add(newEvent);
            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Order {OrderId} status updated to {Status}",
                order.Id,
                OrderStatus.Processing
            );

            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processing cancelled. Requeuing message...");
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing message. Requeuing...");
            
            await _channel!.BasicNackAsync(
                deliveryTag: eventArgs.DeliveryTag,
                multiple: false,
                requeue: true,
                cancellationToken
            );
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("OrderEventConsumerWorker stopping...");
        
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }

        await base.StopAsync(cancellationToken);
    }
}
