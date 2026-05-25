namespace SlimTrack.Models;

public class TicketEvent
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public TicketStatus Status { get; set; }
    public string? Message { get; set; }
    public string? Metadata { get; set; }
    public DateTime Timestamp { get; set; }
}
