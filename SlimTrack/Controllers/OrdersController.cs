using Microsoft.AspNetCore.Mvc;
using SlimTrack.DTOs;
using SlimTrack.Models;

namespace SlimTrack.Controllers;

[ApiController]
[Route("api/[controller]")]
public class OrdersController : ControllerBase
{
    private readonly ILogger<OrdersController> _logger;
    private static readonly List<Order> _orders = [];
    private static readonly object _lock = new();

    public OrdersController(ILogger<OrdersController> logger)
    {
        _logger = logger;
    }

    [HttpPost]
    public IActionResult CreateOrder([FromBody] CreateOrderRequest request)
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

        lock (_lock)
        {
            _orders.Add(order);
            _logger.LogInformation("Order added to list. Total orders: {Count}", _orders.Count);
        }

        _logger.LogInformation("Order {OrderId} created", order.Id);

        var response = MapToResponse(order);
        return CreatedAtAction(nameof(GetOrderById), new { id = order.Id }, response);
    }

    [HttpGet("{id}")]
    public IActionResult GetOrderById(Guid id)
    {
        Order? order;
        lock (_lock)
        {
            order = _orders.FirstOrDefault(o => o.Id == id);
        }

        if (order == null)
        {
            return NotFound(new { message = "Pedido não encontrado" });
        }

        var response = MapToResponse(order);
        return Ok(response);
    }

    [HttpGet("{id}/events")]
    public IActionResult GetOrderEvents(Guid id)
    {
        Order? order;
        lock (_lock)
        {
            order = _orders.FirstOrDefault(o => o.Id == id);
        }

        if (order == null)
        {
            return NotFound(new { message = "Pedido não encontrado" });
        }

        var events = order.Events.Select(e => new OrderEventResponse
        {
            Id = e.Id,
            OrderId = e.OrderId,
            Status = e.Status,
            Message = e.Message,
            Timestamp = e.Timestamp
        }).OrderBy(e => e.Timestamp);

        return Ok(events);
    }

    [HttpGet]
    public IActionResult GetAllOrders([FromQuery] int page = 1, [FromQuery] int pageSize = 10)
    {
        _logger.LogInformation("GetAllOrders called. Current total: {Count}", _orders.Count);
        
        if (page < 1 || pageSize < 1 || pageSize > 100)
        {
            return BadRequest(new { message = "Parâmetros de paginação inválidos" });
        }

        List<Order> orders;
        lock (_lock)
        {
            orders = _orders
                .OrderByDescending(o => o.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();
        }

        var response = orders.Select(MapToResponse);
        return Ok(new
        {
            page,
            pageSize,
            total = _orders.Count,
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
