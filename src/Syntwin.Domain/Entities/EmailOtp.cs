namespace Syntwin.Domain.Entities;

public sealed class EmailOtp
{
    public long Id { get; set; }

    public Guid? UserId { get; set; }

    public string Email { get; set; } = string.Empty;

    public string OtpHash { get; set; } = string.Empty;

    public string Purpose { get; set; } = "LOGIN_CODE";
    public int AttemptCount { get; set; }

    public int MaxAttempts { get; set; } = 5;

    public DateTimeOffset ExpiresAt { get; set; }

    public DateTimeOffset? UsedAt { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }
}
