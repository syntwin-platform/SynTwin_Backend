namespace Syntwin.Application.Payments.Dtos;

public sealed class VnPayPaymentStatusResponse
{
    public Guid PaymentId { get; set; }

    public string MerchantTransactionRef { get; set; } = string.Empty;

    public string PaymentStatus { get; set; } = string.Empty;

    public string? SubscriptionStatus { get; set; }

    public string? SubscriptionPlan { get; set; }

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public string? ResponseCode { get; set; }

    public string? TransactionStatus { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? PaidAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }
}