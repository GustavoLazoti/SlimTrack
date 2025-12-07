namespace SlimTrack.Models;

public class Order
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public OrderStatus CurrentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public List<OrderEvent> Events { get; set; } = [];
}
