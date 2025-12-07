namespace SlimTrack.Models;

public enum OrderStatus
{
    Received = 1,
    Processing = 2,
    InTransit = 3,
    OutForDelivery = 4,
    Delivered = 5,
    Cancelled = 6
}
