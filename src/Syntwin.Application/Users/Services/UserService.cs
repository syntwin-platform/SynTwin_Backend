using Syntwin.Application.Users.Dtos;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Users.Services;

public sealed class UserService : IUserService
{
    private readonly IUserRepository _userRepository;

    public UserService(IUserRepository userRepository)
    {
        _userRepository = userRepository;
    }

    public async Task<CurrentUserProfileResponse?> GetProfileAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);
        return user is null ? null : ToProfileResponse(user);
    }

    public async Task<CurrentUserProfileResponse?> UpdateProfileAsync(
        Guid userId,
        UpdateCurrentUserRequest request,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            return null;
        }

        user.FullName = NormalizeNullable(request.FullName);
        user.AvatarUrl = NormalizeNullable(request.AvatarUrl);
        user.Timezone = string.IsNullOrWhiteSpace(request.Timezone) ? "UTC" : request.Timezone.Trim();
        user.UpdatedAt = DateTimeOffset.UtcNow;

        await _userRepository.SaveChangesAsync(cancellationToken);

        return ToProfileResponse(user);
    }

    public async Task<CurrentSubscriptionResponse?> GetCurrentSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var subscription = await _userRepository.GetActiveSubscriptionAsync(userId, cancellationToken);
        return subscription?.Plan is null ? null : ToSubscriptionResponse(subscription);
    }

    public async Task<CurrentSubscriptionResponse> UpdateSubscriptionAsync(
        Guid userId,
        UpdateSubscriptionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Enum.TryParse<SubscriptionPlanCode>(request.SubscriptionPlan, true, out var planCode))
        {
            throw new InvalidOperationException("Invalid subscription plan.");
        }

        var plan = await _userRepository.GetSubscriptionPlanByCodeAsync(planCode, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException("Subscription plan is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var subscription = await _userRepository.GetActiveSubscriptionAsync(userId, cancellationToken);

        if (subscription is null)
        {
            subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
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
            UserId = userId,
            SubscriptionId = subscription.Id,
            Provider = PaymentProvider.Mock,
            ProviderTransactionId = $"mock_{Guid.NewGuid():N}",
            Amount = plan.MonthlyPrice,
            Currency = "VND",
            Status = PaymentStatus.Paid,
            PaidAt = now,
            CreatedAt = now
        }, cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        subscription.Plan = plan;
        return ToSubscriptionResponse(subscription);
    }

    private static CurrentUserProfileResponse ToProfileResponse(User user)
    {
        var plan = user.Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan)
            .FirstOrDefault();

        return new CurrentUserProfileResponse
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            SubscriptionPlan = plan?.Code.ToString() ?? SubscriptionPlanCode.Free.ToString(),
            Timezone = user.Timezone,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl
        };
    }

    private static CurrentSubscriptionResponse ToSubscriptionResponse(UserSubscription subscription)
    {
        var plan = subscription.Plan!;

        return new CurrentSubscriptionResponse
        {
            PlanCode = plan.Code.ToString(),
            PlanName = plan.Name,
            MonthlyPrice = plan.MonthlyPrice,
            MaxRobots = plan.MaxRobots,
            CanView3D = plan.CanView3D,
            CanSendCommand = plan.CanSendCommand,
            StartsAt = subscription.StartsAt
        };
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}