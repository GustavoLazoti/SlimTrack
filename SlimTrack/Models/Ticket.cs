namespace SlimTrack.Models;

public class Ticket
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public TicketStatus Status { get; set; }
    public TicketCategory? Category { get; set; }
    public TicketPriority? Priority { get; set; }
    public float? ClassificationConfidence { get; set; }
    public string? ClassificationRationale { get; set; }
    public Guid? CreatedByUserId { get; set; }
    public User? CreatedByUser { get; set; }
    public List<TicketEvent> Events { get; set; } = [];
}
