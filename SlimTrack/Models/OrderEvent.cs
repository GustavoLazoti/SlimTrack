namespace SlimTrack.Models;

public class OrderEvent
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
    public string? Metadata { get; set; }
}
