namespace Syntwin.Application.Payments.Dtos;

public sealed class CreateVnPayCheckoutResponse
{
    public Guid PaymentId { get; set; }

    public string MerchantTransactionRef { get; set; } = string.Empty;

    public string PaymentUrl { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string Currency { get; set; } = "VND";

    public string Status { get; set; } = string.Empty;
}