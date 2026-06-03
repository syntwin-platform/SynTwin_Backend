using Syntwin.Application.Admin.Dtos;
using Syntwin.Application.Admin.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Admin.Services;

public sealed class AdminUserService : IAdminUserService
{
    private readonly IUserRepository _userRepository;

    public AdminUserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<AdminUserListResponse> ListUsersAsync(
        string? search,
        string? role,
        string? status,
        string? plan,
        int page,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        var parsedRole = ParseOptionalEnum<UserRole>(role, "role");
        var parsedStatus = ParseOptionalEnum<UserStatus>(status, "status");
        var parsedPlan = ParseOptionalEnum<SubscriptionPlanCode>(plan, "plan");

        page = Math.Max(page, 1);
        pageSize = Math.Clamp(pageSize, 1, 100);
        var skip = (page - 1) * pageSize;

        var totalItems = await _userRepository.CountUsersAsync(
            search,
            parsedRole,
            parsedStatus,
            parsedPlan,
            cancellationToken);

        var users = await _userRepository.ListUsersAsync(
            search,
            parsedRole,
            parsedStatus,
            parsedPlan,
            skip,
            pageSize,
            cancellationToken);

        return new AdminUserListResponse
        {
            Items = users.Select(ToListItemResponse).ToArray(),
            Page = page,
            PageSize = pageSize,
            TotalItems = totalItems,
            TotalPages = (int)Math.Ceiling(totalItems / (double)pageSize)
        };
    }

    public async Task<AdminUserDetailResponse?> GetUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        return user is null ? null : ToDetailResponse(user);
    }

    public async Task<AdminUserDetailResponse?> UpdateStatusAsync(
        Guid adminUserId,
        Guid targetUserId,
        AdminUpdateUserStatusRequest request,
        CancellationToken cancellationToken = default)
    {
        var newStatus = ParseRequiredEnum<UserStatus>(request.Status, "user status");

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (adminUserId == targetUserId && newStatus != UserStatus.Active)
        {
            throw new InvalidOperationException("Admin cannot deactivate their own account.");
        }

        if (user.Role == UserRole.SuperAdmin &&
            newStatus != UserStatus.Active &&
            !await _userRepository.HasAnotherSuperAdminAsync(user.Id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot deactivate the last SuperAdmin.");
        }

        user.Status = newStatus;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return ToDetailResponse(user);
    }

    public async Task<AdminUserDetailResponse?> UpdateSubscriptionAsync(
        Guid targetUserId,
        AdminUpdateUserSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        var planCode = ParseRequiredEnum<SubscriptionPlanCode>(request.SubscriptionPlan, "subscription plan");
        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        var plan = await _userRepository.GetSubscriptionPlanByCodeAsync(planCode, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException("Subscription plan is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var subscription = await _userRepository.GetActiveSubscriptionAsync(targetUserId, cancellationToken);

        if (subscription is null)
        {
            subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = targetUserId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.Active,
                StartsAt = now,
                CreatedAt = now
            };

            await _userRepository.AddUserSubscriptionAsync(subscription, cancellationToken);
        }
        else
        {
            subscription.PlanId = plan.Id;
            subscription.Plan = plan;
            subscription.UpdatedAt = now;
        }

        await _userRepository.AddPaymentTransactionAsync(new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = targetUserId,
            SubscriptionId = subscription.Id,
            Provider = PaymentProvider.Mock,
            ProviderTransactionId = $"admin_mock_{Guid.NewGuid():N}",
            Amount = plan.MonthlyPrice,
            Currency = "VND",
            Status = PaymentStatus.Paid,
            PaidAt = now,
            CreatedAt = now
        }, cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        subscription.Plan = plan;
        user.Subscriptions.Clear();
        user.Subscriptions.Add(subscription);

        return ToDetailResponse(user);
    }

    public async Task<AdminUserDetailResponse?> UpdateRoleAsync(
        Guid adminUserId,
        Guid targetUserId,
        AdminUpdateUserRoleRequest request,
        CancellationToken cancellationToken = default)
    {
        var newRole = ParseRequiredEnum<UserRole>(request.Role, "user role");

        var user = await _userRepository.GetByIdAsync(targetUserId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        if (adminUserId == targetUserId && newRole != UserRole.SuperAdmin)
        {
            throw new InvalidOperationException("Admin cannot remove their own SuperAdmin role.");
        }

        if (user.Role == UserRole.SuperAdmin &&
            newRole != UserRole.SuperAdmin &&
            !await _userRepository.HasAnotherSuperAdminAsync(user.Id, cancellationToken))
        {
            throw new InvalidOperationException("Cannot demote the last SuperAdmin.");
        }

        user.Role = newRole;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return ToDetailResponse(user);
    }

    private static TEnum? ParseOptionalEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (Enum.TryParse<TEnum>(value, true, out var parsed) &&
            Enum.IsDefined(typeof(TEnum), parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid {fieldName}.");
    }

    private static TEnum ParseRequiredEnum<TEnum>(string? value, string fieldName)
        where TEnum : struct
    {
        if (Enum.TryParse<TEnum>(value, true, out var parsed) &&
            Enum.IsDefined(typeof(TEnum), parsed))
        {
            return parsed;
        }

        throw new InvalidOperationException($"Invalid {fieldName}.");
    }

    private static AdminUserListItemResponse ToListItemResponse(User user)
    {
        return new AdminUserListItemResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            SubscriptionPlan = GetPlanCode(user),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt
        };
    }

    private static AdminUserDetailResponse ToDetailResponse(User user)
    {
        return new AdminUserDetailResponse
        {
            Id = user.Id,
            Email = user.Email,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl,
            Timezone = user.Timezone,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            SubscriptionPlan = GetPlanCode(user),
            LastLoginAt = user.LastLoginAt,
            CreatedAt = user.CreatedAt,
            UpdatedAt = user.UpdatedAt
        };
    }

    private static string GetPlanCode(User user)
    {
        return user.Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan?.Code.ToString())
            .FirstOrDefault() ?? SubscriptionPlanCode.Free.ToString();
    }
}
