namespace SlimTrack.DTOs;

public class TicketResponse
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RequesterName { get; set; } = string.Empty;
    public string RequesterEmail { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Category { get; set; }
    public string? Priority { get; set; }
    public float? ClassificationConfidence { get; set; }
    public string? ClassificationRationale { get; set; }
}
