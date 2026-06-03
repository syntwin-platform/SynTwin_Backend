namespace Syntwin.Infrastructure.Payments.VnPay;

public sealed class VnPayOptions
{
    public string PaymentUrl { get; set; } = "https://sandbox.vnpayment.vn/paymentv2/vpcpay.html";

    public string ReturnUrl { get; set; } = string.Empty;
    public string ClientReturnUrl { get; set; } = "http://localhost:3000/payment/vnpay-return";
    public string IpnUrl { get; set; } = string.Empty;

    public string TmnCode { get; set; } = string.Empty;

    public string HashSecret { get; set; } = string.Empty;

    public string Version { get; set; } = "2.1.0";

    public string Command { get; set; } = "pay";

    public string CurrencyCode { get; set; } = "VND";

    public string Locale { get; set; } = "vn";
}
