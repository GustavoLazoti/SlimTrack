namespace SlimTrack.Models;

public class ClassificationLog
{
    public Guid Id { get; set; }
    public Guid TicketId { get; set; }
    public DateTime Timestamp { get; set; }
    public string InputTitle { get; set; } = string.Empty;
    public string InputDescription { get; set; } = string.Empty;
    public TicketCategory SuggestedCategory { get; set; }
    public TicketPriority SuggestedPriority { get; set; }
    public float Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
    public double ProcessingTimeMs { get; set; }
}
