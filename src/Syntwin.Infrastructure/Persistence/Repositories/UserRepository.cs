using Microsoft.EntityFrameworkCore;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Infrastructure.Persistence;

public sealed class UserRepository : IUserRepository
{
    private readonly SyntwinDbContext _dbContext;

    public UserRepository(SyntwinDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public Task<bool> EmailExistsAsync(
    string email,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .AnyAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByEmailAsync(
        string email,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(user => user.Subscriptions
                .Where(subscription => subscription.Status == SubscriptionStatus.Active))
            .ThenInclude(subscription => subscription.Plan)
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .Include(user => user.Subscriptions
                .Where(subscription => subscription.Status == SubscriptionStatus.Active))
            .ThenInclude(subscription => subscription.Plan)
            .FirstOrDefaultAsync(user => user.Id == userId, cancellationToken);
    }

    public Task<SubscriptionPlan?> GetSubscriptionPlanByCodeAsync(
    SubscriptionPlanCode code,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.SubscriptionPlans
            .AsNoTracking()
            .FirstOrDefaultAsync(
                plan => plan.Code == code && plan.IsActive,
                cancellationToken);
    }

    public async Task<IReadOnlyList<SubscriptionPlan>> GetActiveSubscriptionPlansAsync(
    CancellationToken cancellationToken = default)
    {
        return await _dbContext.SubscriptionPlans
            .AsNoTracking()
            .Where(plan => plan.IsActive)
            .OrderBy(plan => plan.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddAsync(
        User user,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.Users.AddAsync(user, cancellationToken);
    }

    public async Task AddUserSubscriptionAsync(
        UserSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.UserSubscriptions.AddAsync(subscription, cancellationToken);
    }

    public Task SaveChangesAsync(
        CancellationToken cancellationToken = default)
    {
        return _dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task AddEmailOtpAsync(
        EmailOtp emailOtp,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.EmailOtps.AddAsync(emailOtp, cancellationToken);
    }

    public async Task AddRefreshTokenAsync(
    RefreshToken refreshToken,
    CancellationToken cancellationToken = default)
    {
        await _dbContext.RefreshTokens.AddAsync(refreshToken, cancellationToken);
    }

    public Task<RefreshToken?> GetRefreshTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        return _dbContext.RefreshTokens
            .Include(token => token.User!)
                .ThenInclude(user => user.Subscriptions
                    .Where(subscription => subscription.Status == SubscriptionStatus.Active))
                .ThenInclude(subscription => subscription.Plan)
            .FirstOrDefaultAsync(
                token => token.TokenHash == tokenHash,
                cancellationToken);
    }
    public Task<EmailOtp?> GetLatestPendingEmailOtpAsync(
    string email,
    string purpose,
    CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return _dbContext.EmailOtps
            .Where(otp =>
                otp.Email == email &&
                otp.Purpose == purpose &&
                otp.UsedAt == null &&
                otp.ExpiresAt > now)
            .OrderByDescending(otp => otp.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<UserSubscription?> GetActiveSubscriptionAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.UserSubscriptions
            .Include(subscription => subscription.Plan)
            .FirstOrDefaultAsync(
                subscription =>
                    subscription.UserId == userId &&
                    subscription.Status == SubscriptionStatus.Active,
                cancellationToken);
    }

    public async Task AddPaymentTransactionAsync(
        PaymentTransaction paymentTransaction,
        CancellationToken cancellationToken = default)
    {
        await _dbContext.PaymentTransactions.AddAsync(paymentTransaction, cancellationToken);
    }
    public Task<PaymentTransaction?> GetPaymentTransactionByMerchantRefAsync(
    string merchantTransactionRef,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.PaymentTransactions
            .Include(payment => payment.Subscription)
                .ThenInclude(subscription => subscription!.Plan)
            .FirstOrDefaultAsync(
                payment => payment.MerchantTransactionRef == merchantTransactionRef,
                cancellationToken);
    }
    private IQueryable<User> BuildUsersQuery(
    string? search,
    UserRole? role,
    UserStatus? status,
    SubscriptionPlanCode? plan)
    {
        var query = _dbContext.Users
    .AsNoTracking()
    .Include(user => user.Subscriptions
        .Where(subscription => subscription.Status == SubscriptionStatus.Active))
    .ThenInclude(subscription => subscription.Plan)
    .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var keyword = search.Trim();

            query = query.Where(user =>
                user.Email.Contains(keyword) ||
                (user.FullName != null && user.FullName.Contains(keyword)));
        }

        if (role is not null)
        {
            query = query.Where(user => user.Role == role);
        }

        if (status is not null)
        {
            query = query.Where(user => user.Status == status);
        }

        if (plan is not null)
        {
            query = query.Where(user =>
                user.Subscriptions.Any(subscription =>
                    subscription.Status == SubscriptionStatus.Active &&
                    subscription.Plan != null &&
                    subscription.Plan.Code == plan));
        }

        return query;
    }

    public async Task<IReadOnlyList<User>> ListUsersAsync(
    string? search,
    UserRole? role,
    UserStatus? status,
    SubscriptionPlanCode? plan,
    int skip,
    int take,
    CancellationToken cancellationToken = default)
    {
        return await BuildUsersQuery(search, role, status, plan)
            .OrderByDescending(user => user.CreatedAt)
            .Skip(skip)
            .Take(take)
            .ToListAsync(cancellationToken);
    }

    public Task<int> CountUsersAsync(
        string? search,
        UserRole? role,
        UserStatus? status,
        SubscriptionPlanCode? plan,
        CancellationToken cancellationToken = default)
    {
        return BuildUsersQuery(search, role, status, plan)
            .CountAsync(cancellationToken);
    }

    public Task<bool> HasAnotherSuperAdminAsync(
    Guid userId,
    CancellationToken cancellationToken = default)
    {
        return _dbContext.Users
            .AsNoTracking()
            .AnyAsync(
                user =>
                    user.Id != userId &&
                    user.Role == UserRole.SuperAdmin &&
                    user.Status == UserStatus.Active,
                cancellationToken);
    }

    public Task<UserSubscription?> GetPendingPaymentSubscriptionAsync(
    Guid userId,
    int planId,
    CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        return _dbContext.UserSubscriptions
            .Include(subscription => subscription.Plan)
            .FirstOrDefaultAsync(
                subscription =>
                    subscription.UserId == userId &&
                    subscription.PlanId == planId &&
                    subscription.Status == SubscriptionStatus.PendingPayment &&
                    (subscription.EndsAt == null || subscription.EndsAt > now),
                cancellationToken);
    }

}
