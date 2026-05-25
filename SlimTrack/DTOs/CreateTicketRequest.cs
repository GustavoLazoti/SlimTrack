using System.ComponentModel.DataAnnotations;

namespace SlimTrack.DTOs;

public class CreateTicketRequest
{
    [Required]
    [MinLength(3), MaxLength(120)]
    public string Title { get; set; } = string.Empty;

    [Required]
    [MinLength(10), MaxLength(2000)]
    public string Description { get; set; } = string.Empty;

    [Required]
    [MinLength(3), MaxLength(80)]
    public string RequesterName { get; set; } = string.Empty;

    [Required]
    [MinLength(5), MaxLength(160)]
    [EmailAddress]
    public string RequesterEmail { get; set; } = string.Empty;
}
