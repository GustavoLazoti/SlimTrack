using SlimTrack.Models;

namespace SlimTrack.Events;

public record OrderStatusChangedEvent
{
    public Guid OrderId { get; init; }
    public OrderStatus OldStatus { get; init; }
    public OrderStatus NewStatus { get; init; }
    public string? Message { get; init; }
    public DateTime ChangedAt { get; init; }
}
