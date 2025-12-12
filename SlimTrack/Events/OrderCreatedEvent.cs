using SlimTrack.Models;

namespace SlimTrack.Events;

public record OrderCreatedEvent
{
    public Guid OrderId { get; init; }
    public string? Description { get; init; }
    public OrderStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
