using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Auth.Dtos;

public sealed class RegisterRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [StringLength(100)]
    public string? FullName { get; set; }

    [Required]
    [StringLength(50)]
    public string Timezone { get; set; } = "UTC";
}
