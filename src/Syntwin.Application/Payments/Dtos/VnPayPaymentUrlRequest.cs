namespace Syntwin.Application.Payments.Dtos;

public sealed class VnPayPaymentUrlRequest
{
    public string MerchantTransactionRef { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string OrderInfo { get; set; } = string.Empty;

    public string OrderType { get; set; } = "other";

    public string IpAddress { get; set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? ExpiresAt { get; set; }
}
