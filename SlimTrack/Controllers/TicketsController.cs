using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SlimTrack.Data.Database;
using SlimTrack.DTOs;
using SlimTrack.Events;
using SlimTrack.Models;
using SlimTrack.Services;

namespace SlimTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class TicketsController : ControllerBase
{
    private readonly ILogger<TicketsController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public TicketsController(
        ILogger<TicketsController> logger,
        AppDbContext dbContext,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    /// <summary>Abre um novo chamado. A triagem automática ocorre de forma assíncrona.</summary>
    [HttpPost]
    public async Task<IActionResult> CreateTicket([FromBody] CreateTicketRequest request)
    {
        _logger.LogInformation("Recebendo requisição de abertura de chamado de {Email}", request.RequesterEmail.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", ""));

        var ticket = new Ticket
        {
            Id = Guid.NewGuid(),
            Title = request.Title,
            Description = request.Description,
            RequesterName = request.RequesterName,
            RequesterEmail = request.RequesterEmail,
            Status = TicketStatus.EmTriagem,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Evento inicial no histórico
        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            Status = TicketStatus.EmTriagem,
            Message = "Chamado recebido. Aguardando triagem automática.",
            Timestamp = DateTime.UtcNow
        };
        ticket.Events.Add(ticketEvent);

        // Mensagem outbox para publicação confiável no RabbitMQ
        var ticketCreatedEvent = new TicketCreatedEvent
        {
            TicketId = ticket.Id,
            Title = ticket.Title,
            Description = ticket.Description,
            Status = ticket.Status,
            CreatedAt = ticket.CreatedAt
        };

        var outboxMessage = new OutboxMessage
        {
            Id = Guid.NewGuid(),
            EventType = "ticket.created",
            Payload = System.Text.Json.JsonSerializer.Serialize(ticketCreatedEvent),
            Published = false,
            CreatedAt = DateTime.UtcNow,
            RetryCount = 0
        };

        _dbContext.Tickets.Add(ticket);
        _dbContext.OutboxMessages.Add(outboxMessage);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Chamado {TicketId} criado por {Email} — status: EmTriagem",
            ticket.Id, ticket.RequesterEmail.Replace(Environment.NewLine, "").Replace("\n", "").Replace("\r", "")
        );

        return CreatedAtAction(nameof(GetTicketById), new { id = ticket.Id }, MapToResponse(ticket));
    }

    /// <summary>Retorna um chamado pelo ID.</summary>
    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetTicketById(Guid id)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound(new { message = "Chamado não encontrado" });

        return Ok(MapToResponse(ticket));
    }

    /// <summary>Lista todos os chamados com paginação.</summary>
    [HttpGet]
    public async Task<IActionResult> GetAllTickets(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 10,
        [FromQuery] TicketStatus? status = null)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
            return BadRequest(new { message = "Parâmetros de paginação inválidos" });

        var query = _dbContext.Tickets.AsQueryable();

        if (status.HasValue)
            query = query.Where(t => t.Status == status.Value);

        var total = await query.CountAsync();

        var tickets = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation(
            "Listagem de chamados: página {Page}, total {Total}", page, total
        );

        return Ok(new
        {
            page,
            pageSize,
            total,
            data = tickets.Select(MapToResponse)
        });
    }

    /// <summary>Retorna o histórico de eventos de um chamado.</summary>
    [HttpGet("{id:guid}/events")]
    public async Task<IActionResult> GetTicketEvents(Guid id)
    {
        var ticketExists = await _dbContext.Tickets.AnyAsync(t => t.Id == id);
        if (!ticketExists)
            return NotFound(new { message = "Chamado não encontrado" });

        var events = await _dbContext.TicketEvents
            .Where(e => e.TicketId == id)
            .OrderBy(e => e.Timestamp)
            .Select(e => new TicketEventResponse
            {
                Id = e.Id,
                TicketId = e.TicketId,
                Status = e.Status.ToString(),
                Message = e.Message,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        return Ok(events);
    }

    /// <summary>
    /// Atualiza status, categoria e/ou prioridade de um chamado.
    /// Uso administrativo — requer perfil Admin.
    /// </summary>
    [HttpPatch("{id:guid}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> UpdateTicket(Guid id, [FromBody] UpdateTicketRequest request)
    {
        var ticket = await _dbContext.Tickets
            .Include(t => t.Events)
            .FirstOrDefaultAsync(t => t.Id == id);

        if (ticket == null)
            return NotFound(new { message = "Chamado não encontrado" });

        if (!request.Status.HasValue && !request.Category.HasValue && !request.Priority.HasValue)
            return BadRequest(new { message = "Nenhum campo para atualizar foi fornecido" });

        var oldStatus = ticket.Status;
        var changed = false;

        if (request.Status.HasValue && request.Status.Value != ticket.Status)
        {
            ticket.Status = request.Status.Value;
            changed = true;
        }

        if (request.Category.HasValue)
        {
            ticket.Category = request.Category.Value;
            changed = true;
        }

        if (request.Priority.HasValue)
        {
            ticket.Priority = request.Priority.Value;
            changed = true;
        }

        if (!changed)
            return Ok(MapToResponse(ticket));

        ticket.UpdatedAt = DateTime.UtcNow;

        var eventMessage = BuildUpdateEventMessage(request, oldStatus);
        var ticketEvent = new TicketEvent
        {
            Id = Guid.NewGuid(),
            TicketId = ticket.Id,
            Status = ticket.Status,
            Message = eventMessage,
            Timestamp = DateTime.UtcNow
        };
        ticket.Events.Add(ticketEvent);

        await _dbContext.SaveChangesAsync();

        _logger.LogInformation(
            "Chamado {TicketId} atualizado pelo administrador — status: {Status}",
            ticket.Id, ticket.Status
        );

        return Ok(MapToResponse(ticket));
    }

    private static string BuildUpdateEventMessage(UpdateTicketRequest request, TicketStatus oldStatus)
    {
        var parts = new List<string>();
        if (request.Status.HasValue)
            parts.Add($"Status alterado de {oldStatus} para {request.Status.Value}");
        if (request.Category.HasValue)
            parts.Add($"Categoria corrigida para {request.Category.Value}");
        if (request.Priority.HasValue)
            parts.Add($"Prioridade corrigida para {request.Priority.Value}");
        return string.Join(". ", parts) + " (alteração administrativa).";
    }

    private static TicketResponse MapToResponse(Ticket ticket) => new()
    {
        Id = ticket.Id,
        Title = ticket.Title,
        Description = ticket.Description,
        RequesterName = ticket.RequesterName,
        RequesterEmail = ticket.RequesterEmail,
        CreatedAt = ticket.CreatedAt,
        Status = ticket.Status.ToString(),
        Category = ticket.Category?.ToString(),
        Priority = ticket.Priority?.ToString(),
        ClassificationConfidence = ticket.ClassificationConfidence,
        ClassificationRationale = ticket.ClassificationRationale
    };
}
