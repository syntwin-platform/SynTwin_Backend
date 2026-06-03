using Syntwin.Application.Admin.Dtos;

namespace Syntwin.Application.Admin.Interfaces;

public interface IAdminUserService
{
    Task<AdminUserListResponse> ListUsersAsync(
        string? search,
        string? role,
        string? status,
        string? plan,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse?> UpdateStatusAsync(
        Guid adminUserId,
        Guid targetUserId,
        AdminUpdateUserStatusRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse?> UpdateRoleAsync(
        Guid adminUserId,
        Guid targetUserId,
        AdminUpdateUserRoleRequest request,
        CancellationToken cancellationToken = default);

    Task<AdminUserDetailResponse?> UpdateSubscriptionAsync(
        Guid targetUserId,
        AdminUpdateUserSubscriptionRequest request,
        CancellationToken cancellationToken = default);
}
