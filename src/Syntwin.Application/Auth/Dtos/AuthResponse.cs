namespace Syntwin.Application.Auth.Dtos;

public sealed class AuthResponse
{
    public string AccessToken { get; set; } = string.Empty;

    public string RefreshToken { get; set; } = string.Empty;

    public DateTimeOffset ExpiresAt { get; set; }

    public CurrentUserResponse User { get; set; } = new();
}