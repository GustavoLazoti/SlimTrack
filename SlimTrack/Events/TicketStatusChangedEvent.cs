using SlimTrack.Models;

namespace SlimTrack.Events;

public record TicketStatusChangedEvent
{
    public Guid TicketId { get; init; }
    public TicketStatus OldStatus { get; init; }
    public TicketStatus NewStatus { get; init; }
    public string? Message { get; init; }
    public DateTime ChangedAt { get; init; }
}
