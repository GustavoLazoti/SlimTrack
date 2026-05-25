using SlimTrack.Models;

namespace SlimTrack.Services;

public interface IClassificationService
{
    ClassificationResult Classify(string title, string description);
}

public class ClassificationResult
{
    public TicketCategory Category { get; set; }
    public TicketPriority Priority { get; set; }
    public float Confidence { get; set; }
    public string Rationale { get; set; } = string.Empty;
}
