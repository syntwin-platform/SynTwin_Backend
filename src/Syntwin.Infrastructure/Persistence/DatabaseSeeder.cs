using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using Syntwin.Infrastructure.Persistence;

namespace Syntwin.Infrastructure;

public static class DatabaseSeeder
{
    public static async Task SeedSuperAdminAsync(
        this IServiceProvider services,
        IConfiguration configuration,
        CancellationToken cancellationToken = default)
    {
        var section = configuration.GetSection("Seed:SuperAdmin");

        if (!bool.TryParse(section["Enabled"], out var enabled) || !enabled)
        {
            return;
        }

        var email = NormalizeEmail(section["Email"]);
        var password = section["Password"] ?? string.Empty;
        var fullName = NormalizeNullableText(section["FullName"]) ?? "SynTwin Admin";
        var timezone = string.IsNullOrWhiteSpace(section["Timezone"])
            ? "UTC"
            : section["Timezone"]!.Trim();

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Seed:SuperAdmin:Email is required when SuperAdmin seed is enabled.");
        }

        if (password.Length < 8)
        {
            throw new InvalidOperationException("Seed:SuperAdmin:Password must be at least 8 characters.");
        }

        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<SyntwinDbContext>();

        var existingUser = await dbContext.Users
            .FirstOrDefaultAsync(user => user.Email == email, cancellationToken);

        if (existingUser is not null)
        {
            var changed = false;

            if (existingUser.Role != UserRole.SuperAdmin)
            {
                existingUser.Role = UserRole.SuperAdmin;
                changed = true;
            }

            if (existingUser.Status != UserStatus.Active)
            {
                existingUser.Status = UserStatus.Active;
                changed = true;
            }

            if (changed)
            {
                existingUser.UpdatedAt = DateTimeOffset.UtcNow;
                await dbContext.SaveChangesAsync(cancellationToken);
            }

            return;
        }

        var freePlan = await dbContext.SubscriptionPlans
            .FirstOrDefaultAsync(
                plan => plan.Code == SubscriptionPlanCode.Free && plan.IsActive,
                cancellationToken);

        if (freePlan is null)
        {
            throw new InvalidOperationException("FREE subscription plan is not configured.");
        }

        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Role = UserRole.SuperAdmin,
            Status = UserStatus.Active,
            FullName = fullName,
            Timezone = timezone,
            CreatedAt = now
        };

        user.Subscriptions.Add(new UserSubscription
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            PlanId = freePlan.Id,
            Status = SubscriptionStatus.Active,
            StartsAt = now,
            AutoRenew = false,
            CreatedAt = now,
            User = user,
            Plan = freePlan
        });

        await dbContext.Users.AddAsync(user, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
