using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Auth.Dtos;

public sealed class LoginCodeRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;
}
