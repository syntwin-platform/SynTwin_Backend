namespace Syntwin.Application.Auth.Dtos;

public sealed class GeneratedJwtToken
{
    public string AccessToken { get; set; } = string.Empty;

    public string JwtId { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }
}