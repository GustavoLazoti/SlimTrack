using SlimTrack.Models;

namespace SlimTrack.Events;

public record TicketCreatedEvent
{
    public Guid TicketId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public TicketStatus Status { get; init; }
    public DateTime CreatedAt { get; init; }
}
