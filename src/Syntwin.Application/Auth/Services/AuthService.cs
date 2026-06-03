using System.Security.Cryptography;
using System.Text;
using Syntwin.Application.Auth.Dtos;
using Syntwin.Application.Auth.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Auth.Services;

public sealed class AuthService : IAuthService
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IJwtTokenGenerator _jwtTokenGenerator;
    private readonly IEmailSender _emailSender;
    public AuthService(
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IJwtTokenGenerator jwtTokenGenerator,
    IEmailSender emailSender)
    {
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _jwtTokenGenerator = jwtTokenGenerator;
        _emailSender = emailSender;
    }

    public async Task<AuthResponse> RegisterAsync(
        RegisterRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (await _userRepository.EmailExistsAsync(email, cancellationToken))
        {
            throw new InvalidOperationException("Email already exists.");
        }

        var freePlan = await _userRepository.GetSubscriptionPlanByCodeAsync(
            SubscriptionPlanCode.Free,
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
            PasswordHash = _passwordHasher.Hash(request.Password),
            Role = UserRole.User,
            Status = UserStatus.Active,
            Timezone = NormalizeTimezone(request.Timezone),
            FullName = NormalizeNullableText(request.FullName),
            CreatedAt = now
        };

        var subscription = new UserSubscription
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
        };

        user.Subscriptions.Add(subscription);

        await _userRepository.AddAsync(user, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return await CreateAuthResponseAsync(user, cancellationToken);
    }

    public async Task<AuthResponse> LoginAsync(
        LoginRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("User account is not active.");
        }

        var passwordValid = _passwordHasher.Verify(
            request.Password,
            user.PasswordHash);

        if (!passwordValid)
        {
            throw new InvalidOperationException("Invalid email or password.");
        }

        user.LastLoginAt = DateTimeOffset.UtcNow;
        user.UpdatedAt = DateTimeOffset.UtcNow;

        var response = await CreateAuthResponseAsync(user, cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);
        await SendLoginNotificationAsync(user, "password", cancellationToken);

        return response;
    }

    public async Task<CurrentUserResponse?> GetCurrentUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        return user is null
            ? null
            : CreateCurrentUserResponse(user);
    }

    private async Task<AuthResponse> CreateAuthResponseAsync(
      User user,
      CancellationToken cancellationToken)
    {
        var token = _jwtTokenGenerator.Generate(user);
        var rawRefreshToken = GenerateSecureToken();

        var refreshToken = new RefreshToken
        {
            Id = Guid.NewGuid(),
            UserId = user.Id,
            TokenHash = HashToken(rawRefreshToken),
            JwtId = token.JwtId,
            ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
            CreatedAt = DateTimeOffset.UtcNow,
            User = user
        };

        await _userRepository.AddRefreshTokenAsync(refreshToken, cancellationToken);

        return new AuthResponse
        {
            AccessToken = token.AccessToken,
            RefreshToken = rawRefreshToken,
            ExpiresAt = token.ExpiresAt,
            User = CreateCurrentUserResponse(user)
        };
    }

    private static CurrentUserResponse CreateCurrentUserResponse(User user)
    {
        var plan = GetActivePlan(user);

        return new CurrentUserResponse
        {
            Id = user.Id,
            Email = user.Email,
            Role = user.Role.ToString(),
            Status = user.Status.ToString(),
            SubscriptionPlan = plan?.Code.ToString() ?? SubscriptionPlanCode.Free.ToString(),
            CanView3D = plan?.CanView3D ?? false,
            CanSendCommand = plan?.CanSendCommand ?? false,
            MaxRobots = plan?.MaxRobots ?? 1,
            Timezone = user.Timezone,
            FullName = user.FullName,
            AvatarUrl = user.AvatarUrl
        };
    }

    private static SubscriptionPlan? GetActivePlan(User user)
    {
        return user.Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan)
            .FirstOrDefault();
    }

    private static string NormalizeEmail(string? email)
    {
        return string.IsNullOrWhiteSpace(email)
            ? string.Empty
            : email.Trim().ToLowerInvariant();
    }

    private static string NormalizeTimezone(string timezone)
    {
        return string.IsNullOrWhiteSpace(timezone)
            ? "UTC"
            : timezone.Trim();
    }

    private static string? NormalizeNullableText(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }

    private static string GenerateSecureToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);

        return Convert.ToBase64String(bytes)
            .Replace("+", "-", StringComparison.Ordinal)
            .Replace("/", "_", StringComparison.Ordinal)
            .Replace("=", string.Empty, StringComparison.Ordinal);
    }

    private static string HashToken(string token)
    {
        var bytes = Encoding.UTF8.GetBytes(token);
        var hash = SHA256.HashData(bytes);

        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    public async Task<AuthResponse> RefreshAsync(
    RefreshTokenRequest request,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        var tokenHash = HashToken(request.RefreshToken);

        var storedToken = await _userRepository.GetRefreshTokenByHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null ||
            storedToken.RevokedAt is not null ||
            storedToken.ExpiresAt <= DateTimeOffset.UtcNow ||
            storedToken.User is null)
        {
            throw new InvalidOperationException("Invalid or expired refresh token.");
        }

        if (storedToken.User.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("User account is not active.");
        }

        storedToken.RevokedAt = DateTimeOffset.UtcNow;

        var response = await CreateAuthResponseAsync(storedToken.User, cancellationToken);

        storedToken.ReplacedByTokenHash = HashToken(response.RefreshToken);

        await _userRepository.SaveChangesAsync(cancellationToken);

        return response;
    }
    public async Task LogoutAsync(
    LogoutRequest request,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RefreshToken))
        {
            return;
        }

        var tokenHash = HashToken(request.RefreshToken);

        var storedToken = await _userRepository.GetRefreshTokenByHashAsync(
            tokenHash,
            cancellationToken);

        if (storedToken is null)
        {
            return;
        }

        if (storedToken.RevokedAt is null)
        {
            storedToken.RevokedAt = DateTimeOffset.UtcNow;
            await _userRepository.SaveChangesAsync(cancellationToken);
        }
    }
    public async Task<MessageResponse> RequestLoginCodeAsync(
    LoginCodeRequest request,
    CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);

        if (string.IsNullOrWhiteSpace(email))
        {
            throw new InvalidOperationException("Email is required.");
        }

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        var genericResponse = new MessageResponse
        {
            Message = "If the email exists, a login code has been sent."
        };

        if (user is null || user.Status != UserStatus.Active)
        {
            return genericResponse;
        }
        var now = DateTimeOffset.UtcNow;

        var latestPendingOtp = await _userRepository.GetLatestPendingEmailOtpAsync(
            email,
            "LOGIN_CODE",
            cancellationToken);

        if (latestPendingOtp is not null &&
            latestPendingOtp.CreatedAt > now.AddMinutes(-1))
        {
            return genericResponse;
        }
        var code = RandomNumberGenerator.GetInt32(100000, 1000000).ToString();
        var emailOtp = new EmailOtp
        {
            Email = email,
            UserId = user.Id,
            OtpHash = HashToken($"{email}:LOGIN_CODE:{code}"),
            Purpose = "LOGIN_CODE",
            ExpiresAt = now.AddMinutes(10),
            CreatedAt = now,
            AttemptCount = 0,
            MaxAttempts = 5
        };

        await _userRepository.AddEmailOtpAsync(emailOtp, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        await _emailSender.SendAsync(
            email,
            "Your SynTwin login code",
            $"<p>Your SynTwin login code is:</p><h2>{code}</h2><p>This code expires in 10 minutes.</p>",
            cancellationToken);

        return genericResponse;
    }

    public async Task<AuthResponse> ConfirmLoginCodeAsync(
        LoginCodeConfirmRequest request,
        CancellationToken cancellationToken = default)
    {
        var email = NormalizeEmail(request.Email);
        var code = request.Code?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(code))
        {
            throw new InvalidOperationException("Email and code are required.");
        }

        var emailOtp = await _userRepository.GetLatestPendingEmailOtpAsync(
            email,
            "LOGIN_CODE",
            cancellationToken);

        if (emailOtp is null || emailOtp.AttemptCount >= emailOtp.MaxAttempts)
        {
            throw new InvalidOperationException("Invalid or expired login code.");
        }

        var codeHash = HashToken($"{email}:LOGIN_CODE:{code}");

        if (emailOtp.OtpHash != codeHash)
        {
            emailOtp.AttemptCount += 1;
            await _userRepository.SaveChangesAsync(cancellationToken);

            throw new InvalidOperationException("Invalid or expired login code.");
        }

        var user = await _userRepository.GetByEmailAsync(email, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("Invalid or expired login code.");
        }

        if (user.Status != UserStatus.Active)
        {
            throw new InvalidOperationException("User account is not active.");
        }

        var now = DateTimeOffset.UtcNow;

        emailOtp.UsedAt = now;
        user.LastLoginAt = now;
        user.UpdatedAt = now;

        var response = await CreateAuthResponseAsync(user, cancellationToken);

        await _userRepository.SaveChangesAsync(cancellationToken);
        await SendLoginNotificationAsync(user, "email code", cancellationToken);

        return response;
    }
    private async Task SendLoginNotificationAsync(
    User user,
    string method,
    CancellationToken cancellationToken)
    {
        await _emailSender.SendAsync(
            user.Email,
            "New SynTwin login",
            $"""
        <p>Your SynTwin account was just signed in.</p>
        <p><strong>Login method:</strong> {method}</p>
        <p><strong>Time:</strong> {DateTimeOffset.UtcNow:yyyy-MM-dd HH:mm:ss} UTC</p>
        <p>If this was not you, please change your password immediately.</p>
        """,
            cancellationToken);
    }
}
