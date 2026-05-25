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
/// Worker responsável pela classificação automática de chamados.
/// Consome eventos de criação de chamado (ticket.created) e executa
/// o serviço de triagem automática, atualizando o status para Aberto.
/// </summary>
public class TicketClassificationWorker : BackgroundService
{
    private readonly IConnection _connection;
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<TicketClassificationWorker> _logger;
    private IChannel? _channel;

    private const string ExchangeName = "tickets";
    private const string QueueName = "tickets.classification";
    private const string RoutingKey = "ticket.created";

    public TicketClassificationWorker(
        IConnection connection,
        IServiceProvider serviceProvider,
        ILogger<TicketClassificationWorker> logger)
    {
        _connection = connection;
        _serviceProvider = serviceProvider;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("TicketClassificationWorker iniciando...");

        try
        {
            _channel = await _connection.CreateChannelAsync(cancellationToken: stoppingToken);

            // Processa um chamado por vez para evitar sobrecarga
            await _channel.BasicQosAsync(
                prefetchSize: 0,
                prefetchCount: 1,
                global: false,
                cancellationToken: stoppingToken
            );

            await _channel.ExchangeDeclareAsync(
                exchange: ExchangeName,
                type: ExchangeType.Topic,
                durable: true,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            await _channel.QueueDeclareAsync(
                queue: QueueName,
                durable: true,
                exclusive: false,
                autoDelete: false,
                arguments: null,
                cancellationToken: stoppingToken
            );

            await _channel.QueueBindAsync(
                queue: QueueName,
                exchange: ExchangeName,
                routingKey: RoutingKey,
                arguments: null,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation(
                "Fila '{Queue}' vinculada ao exchange '{Exchange}' com routing key '{RoutingKey}'",
                QueueName, ExchangeName, RoutingKey
            );

            var consumer = new AsyncEventingBasicConsumer(_channel);
            consumer.ReceivedAsync += async (_, eventArgs) =>
                await ProcessMessageAsync(eventArgs, stoppingToken);

            await _channel.BasicConsumeAsync(
                queue: QueueName,
                autoAck: false,
                consumer: consumer,
                cancellationToken: stoppingToken
            );

            _logger.LogInformation("TicketClassificationWorker aguardando chamados para classificar...");
            await Task.Delay(Timeout.Infinite, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("TicketClassificationWorker encerrando...");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro fatal no TicketClassificationWorker");
            throw;
        }
    }

    private async Task ProcessMessageAsync(BasicDeliverEventArgs eventArgs, CancellationToken cancellationToken)
    {
        var messageBody = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        _logger.LogInformation("Chamado recebido para classificação: {Message}", messageBody);

        try
        {
            var ticketCreatedEvent = JsonSerializer.Deserialize<TicketCreatedEvent>(messageBody);

            if (ticketCreatedEvent == null)
            {
                _logger.LogWarning("Falha ao desserializar evento. Rejeitando mensagem...");
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var classificationService = scope.ServiceProvider.GetRequiredService<IClassificationService>();
            var eventPublisher = scope.ServiceProvider.GetRequiredService<IEventPublisher>();

            var ticket = await dbContext.Tickets
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == ticketCreatedEvent.TicketId, cancellationToken);

            if (ticket == null)
            {
                _logger.LogWarning("Chamado {TicketId} não encontrado. Rejeitando...", ticketCreatedEvent.TicketId);
                await _channel!.BasicRejectAsync(eventArgs.DeliveryTag, requeue: false, cancellationToken);
                return;
            }

            if (ticket.Status != TicketStatus.EmTriagem)
            {
                _logger.LogWarning(
                    "Chamado {TicketId} já processado (status: {Status}). Confirmando...",
                    ticket.Id, ticket.Status
                );
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            _logger.LogInformation("Classificando chamado {TicketId}...", ticket.Id);

            var startTime = DateTime.UtcNow;
            var result = classificationService.Classify(ticket.Title, ticket.Description);
            var processingMs = (DateTime.UtcNow - startTime).TotalMilliseconds;

            // Atualizar ticket com classificação (otimista, evita conflito concorrente)
            var rowsAffected = await dbContext.Database.ExecuteSqlAsync(
                $@"UPDATE ""Tickets""
                   SET ""Status""                   = {(int)TicketStatus.Aberto},
                       ""Category""                 = {(int)result.Category},
                       ""Priority""                 = {(int)result.Priority},
                       ""ClassificationConfidence"" = {result.Confidence},
                       ""ClassificationRationale""  = {result.Rationale},
                       ""UpdatedAt""                = {DateTime.UtcNow}
                   WHERE ""Id"" = {ticket.Id}
                   AND ""Status"" = {(int)TicketStatus.EmTriagem}",
                cancellationToken
            );

            if (rowsAffected == 0)
            {
                _logger.LogWarning("Chamado {TicketId} já foi atualizado por outro processo.", ticket.Id);
                await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
                return;
            }

            // Registrar evento de mudança de status
            var ticketEvent = new TicketEvent
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                Status = TicketStatus.Aberto,
                Message = $"Triagem automática concluída. Categoria: {result.Category}, Prioridade: {result.Priority}",
                Timestamp = DateTime.UtcNow
            };
            dbContext.TicketEvents.Add(ticketEvent);

            // Registrar log de classificação para auditoria
            var classificationLog = new ClassificationLog
            {
                Id = Guid.NewGuid(),
                TicketId = ticket.Id,
                Timestamp = DateTime.UtcNow,
                InputTitle = ticket.Title,
                InputDescription = ticket.Description,
                SuggestedCategory = result.Category,
                SuggestedPriority = result.Priority,
                Confidence = result.Confidence,
                Rationale = result.Rationale,
                ProcessingTimeMs = processingMs
            };
            dbContext.ClassificationLogs.Add(classificationLog);

            await dbContext.SaveChangesAsync(cancellationToken);

            _logger.LogInformation(
                "Chamado {TicketId} classificado: {Category} / {Priority} (confiança: {Confidence:P0})",
                ticket.Id, result.Category, result.Priority, result.Confidence
            );

            // Publicar evento de status atualizado
            var statusChangedEvent = new TicketStatusChangedEvent
            {
                TicketId = ticket.Id,
                OldStatus = TicketStatus.EmTriagem,
                NewStatus = TicketStatus.Aberto,
                Message = result.Rationale,
                ChangedAt = DateTime.UtcNow
            };
            await eventPublisher.PublishAsync(ExchangeName, "ticket.status_changed", statusChangedEvent);

            await _channel!.BasicAckAsync(eventArgs.DeliveryTag, multiple: false, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Processamento cancelado. Recolocando na fila...");
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao classificar chamado. Recolocando na fila...");
            await _channel!.BasicNackAsync(eventArgs.DeliveryTag, multiple: false, requeue: true, cancellationToken);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("TicketClassificationWorker encerrando...");
        if (_channel != null)
        {
            await _channel.CloseAsync(cancellationToken);
            _channel.Dispose();
        }
        await base.StopAsync(cancellationToken);
    }
}
