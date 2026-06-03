using Syntwin.Application.Auth.Dtos;

namespace Syntwin.Application.Auth.Interfaces;

public interface IAuthService
{
    Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentUserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AuthResponse> RefreshAsync(
    RefreshTokenRequest request,
    CancellationToken cancellationToken = default);

    Task LogoutAsync(
        LogoutRequest request,
        CancellationToken cancellationToken = default);

    Task<MessageResponse> RequestLoginCodeAsync(
    LoginCodeRequest request,
    CancellationToken cancellationToken = default);

    Task<AuthResponse> ConfirmLoginCodeAsync(
        LoginCodeConfirmRequest request,
        CancellationToken cancellationToken = default);
}
