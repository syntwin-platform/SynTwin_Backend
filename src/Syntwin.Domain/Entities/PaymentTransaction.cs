using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class PaymentTransaction
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid? SubscriptionId { get; set; }

    public PaymentProvider Provider { get; set; } = PaymentProvider.Mock;

    public string? MerchantTransactionRef { get; set; }

    public string? ProviderTransactionId { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public PaymentStatus Status { get; set; } = PaymentStatus.Pending;

    public string? ResponseCode { get; set; }

    public string? TransactionStatus { get; set; }

    public string? BankCode { get; set; }

    public DateTimeOffset? PayDate { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public string? FailureReason { get; set; }

    public string? RawPayloadJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public User? User { get; set; }

    public UserSubscription? Subscription { get; set; }
}
