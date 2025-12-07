using SlimTrack.Models;

namespace SlimTrack.DTOs;

public class OrderResponse
{
    public Guid Id { get; set; }
    public string? Description { get; set; }
    public OrderStatus CurrentStatus { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
