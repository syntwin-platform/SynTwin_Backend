using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Auth.Dtos;

public sealed class RefreshTokenRequest
{
    [Required]
    [StringLength(512)]
    public string RefreshToken { get; set; } = string.Empty;
}
