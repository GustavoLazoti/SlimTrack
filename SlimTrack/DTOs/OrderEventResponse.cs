using SlimTrack.Models;

namespace SlimTrack.DTOs;

public class OrderEventResponse
{
    public Guid Id { get; set; }
    public Guid OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}
