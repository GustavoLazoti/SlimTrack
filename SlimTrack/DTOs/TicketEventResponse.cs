namespace SlimTrack.DTOs;

public class TicketEventResponse
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public DateTime Timestamp { get; set; }
}
