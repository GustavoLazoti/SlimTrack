using SlimTrack.Models;

namespace SlimTrack.DTOs;

public class UpdateTicketRequest
{
    public TicketStatus? Status { get; set; }
    public TicketCategory? Category { get; set; }
    public TicketPriority? Priority { get; set; }
}
