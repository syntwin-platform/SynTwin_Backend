using Syntwin.Application.Users.Dtos;

namespace Syntwin.Application.Users.Interfaces;

public interface IUserService
{
    Task<CurrentUserProfileResponse?> GetProfileAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CurrentUserProfileResponse?> UpdateProfileAsync(
        Guid userId,
        UpdateCurrentUserRequest request,
        CancellationToken cancellationToken = default);

    Task<CurrentSubscriptionResponse?> GetCurrentSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<CurrentSubscriptionResponse> UpdateSubscriptionAsync(
        Guid userId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken = default);
}
