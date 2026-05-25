using System.ComponentModel.DataAnnotations;

namespace SlimTrack.DTOs;

public class RegisterRequest
{
    [Required]
    [MinLength(3), MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(200)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [MinLength(6), MaxLength(100)]
    public string Password { get; set; } = string.Empty;
}
