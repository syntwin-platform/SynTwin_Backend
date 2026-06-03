using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Users.Interfaces;

public interface IUserRepository
{
    Task<bool> EmailExistsAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default);

    Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<SubscriptionPlan?> GetSubscriptionPlanByCodeAsync(
        SubscriptionPlanCode code,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SubscriptionPlan>> GetActiveSubscriptionPlansAsync(
        CancellationToken cancellationToken = default);

    Task AddAsync(
        User user,
        CancellationToken cancellationToken = default);

    Task AddUserSubscriptionAsync(
        UserSubscription subscription,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(
        CancellationToken cancellationToken = default);

    Task AddEmailOtpAsync(
        EmailOtp emailOtp,
        CancellationToken cancellationToken = default);

    Task AddRefreshTokenAsync(
    RefreshToken refreshToken,
    CancellationToken cancellationToken = default);

    Task<RefreshToken?> GetRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default);

    Task<EmailOtp?> GetLatestPendingEmailOtpAsync(
    string email,
    string purpose,
    CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetActiveSubscriptionAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    Task<UserSubscription?> GetPendingPaymentSubscriptionAsync(
        Guid userId,
        int planId,
        CancellationToken cancellationToken = default);

    Task AddPaymentTransactionAsync(
        PaymentTransaction paymentTransaction,
        CancellationToken cancellationToken = default);

    Task<PaymentTransaction?> GetPaymentTransactionByMerchantRefAsync(
    string merchantTransactionRef,
    CancellationToken cancellationToken = default);

    Task<IReadOnlyList<User>> ListUsersAsync(
    string? search,
    UserRole? role,
    UserStatus? status,
    SubscriptionPlanCode? plan,
    int skip,
    int take,
    CancellationToken cancellationToken = default);

    Task<int> CountUsersAsync(
        string? search,
        UserRole? role,
        UserStatus? status,
        SubscriptionPlanCode? plan,
        CancellationToken cancellationToken = default);

    Task<bool> HasAnotherSuperAdminAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}
