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
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;
    private readonly AppDbContext _dbContext;
    private readonly IEventPublisher _eventPublisher;

    public OrdersController(
        ILogger<OrdersController> logger,
        AppDbContext dbContext,
        IEventPublisher eventPublisher)
    {
        _logger = logger;
        _dbContext = dbContext;
        _eventPublisher = eventPublisher;
    }

    [HttpPost]
    public async Task<IActionResult> CreateOrder([FromBody] CreateOrderRequest request)
    {
        _logger.LogInformation("Receiving order creation request");
        
        var order = new Order
        {
            Id = Guid.NewGuid(),
            Description = request.Description,
            CurrentStatus = OrderStatus.Received,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        var orderEvent = new OrderEvent
        {
            Id = Guid.NewGuid(),
            OrderId = order.Id,
            Status = OrderStatus.Received,
            Message = "Pedido recebido com sucesso",
            Timestamp = DateTime.UtcNow
        };

        order.Events.Add(orderEvent);

        _dbContext.Orders.Add(order);
        await _dbContext.SaveChangesAsync();

        _logger.LogInformation("Order {OrderId} created and saved to PostgreSQL", order.Id);

        var orderCreatedEvent = new OrderCreatedEvent
        {
            OrderId = order.Id,
            Description = order.Description,
            Status = order.CurrentStatus,
            CreatedAt = order.CreatedAt
        };

        await _eventPublisher.PublishAsync(
            exchange: "orders",
            routingKey: "order.created",
            @event: orderCreatedEvent
        );

        _logger.LogInformation("Order {OrderId} event published to RabbitMQ", order.Id);

        var response = MapToResponse(order);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetOrderById(Guid id)
    {
        var order = await _dbContext.Orders
            .Include(o => o.Events)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null)
        {
            return NotFound(new { message = "Pedido não encontrado" });
        }

        var response = MapToResponse(order);
        return Ok(response);
    }

    [HttpGet("{id}/events")]
    public async Task<IActionResult> GetOrderEvents(Guid id)
    {
        var orderExists = await _dbContext.Orders.AnyAsync(o => o.Id == id);
        if (!orderExists)
        {
            return NotFound(new { message = "Pedido não encontrado" });
        }

        var events = await _dbContext.OrderEvents
            .Where(e => e.OrderId == id)
            .OrderBy(e => e.Timestamp)
            .Select(e => new OrderEventResponse
            {
                Id = e.Id,
                OrderId = e.OrderId,
                Status = e.Status,
                Message = e.Message,
                Timestamp = e.Timestamp
            })
            .ToListAsync();

        return Ok(events);
    }

    [HttpGet]
    public async Task<IActionResult> GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "Parâmetros de paginação inválidos" });
        }

        var total = await _dbContext.Orders.CountAsync();
        
        var orders = await _dbContext.Orders
            .OrderByDescending(o => o.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        _logger.LogInformation("GetAllOrders returned {Count} orders from PostgreSQL (page {Page}, total {Total})", 
            orders.Count, page, total);

        var response = orders.Select(MapToResponse);
        return Ok(new
        {
            page,
            pageSize,
            total,
            data = response
        });
    }

    private static OrderResponse MapToResponse(Order order)
    {
        return new OrderResponse
        {
            Id = order.Id,
            Description = order.Description,
            CurrentStatus = order.CurrentStatus,
            CreatedAt = order.CreatedAt,
            UpdatedAt = order.UpdatedAt
        };
    }
}
