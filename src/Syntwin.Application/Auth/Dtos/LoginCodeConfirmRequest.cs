using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Auth.Dtos;

public sealed class LoginCodeConfirmRequest
{
    [Required]
    [EmailAddress]
    [StringLength(100)]
    public string Email { get; set; } = string.Empty;

    [Required]
    [RegularExpression(@"^\d{6}$")]
    public string Code { get; set; } = string.Empty;
}
