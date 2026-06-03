using System.Globalization;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Syntwin.Application.Payments.Dtos;
using Syntwin.Application.Payments.Interfaces;

namespace Syntwin.Infrastructure.Payments.VnPay;

public sealed class VnPayGateway : IVnPayGateway
{
    private static readonly TimeSpan VietnamOffset = TimeSpan.FromHours(7);
    private readonly VnPayOptions _options;

    public VnPayGateway(IOptions<VnPayOptions> options)
    {
        _options = options.Value;
    }

    public string CreatePaymentUrl(VnPayPaymentUrlRequest request)
    {
        EnsureConfigured();

        var createdAt = request.CreatedAt.ToOffset(VietnamOffset);
        var expiresAt = (request.ExpiresAt ?? request.CreatedAt.AddMinutes(15)).ToOffset(VietnamOffset);
        var amount = decimal.ToInt64(decimal.Round(request.Amount, 0, MidpointRounding.AwayFromZero)) * 100;

        var parameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["vnp_Amount"] = amount.ToString(CultureInfo.InvariantCulture),
            ["vnp_Command"] = _options.Command,
            ["vnp_CreateDate"] = createdAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_CurrCode"] = _options.CurrencyCode,
            ["vnp_ExpireDate"] = expiresAt.ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture),
            ["vnp_IpAddr"] = request.IpAddress,
            ["vnp_Locale"] = _options.Locale,
            ["vnp_OrderInfo"] = request.OrderInfo,
            ["vnp_OrderType"] = request.OrderType,
            ["vnp_ReturnUrl"] = _options.ReturnUrl,
            ["vnp_TmnCode"] = _options.TmnCode,
            ["vnp_TxnRef"] = request.MerchantTransactionRef,
            ["vnp_Version"] = _options.Version
        };

        var query = BuildData(parameters);
        var secureHash = ComputeHmacSha512(query, _options.HashSecret);

        return $"{_options.PaymentUrl}?{query}&vnp_SecureHash={secureHash}";
    }

    public bool IsValidSignature(IReadOnlyDictionary<string, string?> parameters)
    {
        EnsureConfigured();

        if (!parameters.TryGetValue("vnp_SecureHash", out var receivedHash) ||
            string.IsNullOrWhiteSpace(receivedHash))
        {
            return false;
        }

        var signedParameters = new SortedDictionary<string, string>(StringComparer.Ordinal);

        foreach (var parameter in parameters)
        {
            if (parameter.Key.Equals("vnp_SecureHash", StringComparison.OrdinalIgnoreCase) ||
                parameter.Key.Equals("vnp_SecureHashType", StringComparison.OrdinalIgnoreCase) ||
                string.IsNullOrEmpty(parameter.Value))
            {
                continue;
            }

            signedParameters[parameter.Key] = parameter.Value;
        }

        var signData = BuildData(signedParameters);
        var expectedHash = ComputeHmacSha512(signData, _options.HashSecret);

        return CryptographicOperations.FixedTimeEquals(
            Encoding.UTF8.GetBytes(expectedHash.ToUpperInvariant()),
            Encoding.UTF8.GetBytes(receivedHash.ToUpperInvariant()));
    }

    public string CreateReturnRedirectUrl(IReadOnlyDictionary<string, string?> parameters)
    {
        var clientReturnUrl = string.IsNullOrWhiteSpace(_options.ClientReturnUrl)
            ? "http://localhost:3000/payment/vnpay-return"
            : _options.ClientReturnUrl;

        var txnRef = GetParameter(parameters, "vnp_TxnRef");
        var responseCode = GetParameter(parameters, "vnp_ResponseCode");
        var transactionStatus = GetParameter(parameters, "vnp_TransactionStatus");

        bool signatureValid;

        try
        {
            signatureValid = IsValidSignature(parameters);
        }
        catch (InvalidOperationException)
        {
            signatureValid = false;
        }

        var status = signatureValid &&
                     responseCode == "00" &&
                     transactionStatus == "00"
            ? "success"
            : "failed";

        var redirectParameters = new SortedDictionary<string, string>(StringComparer.Ordinal)
        {
            ["responseCode"] = responseCode,
            ["signatureValid"] = signatureValid ? "true" : "false",
            ["status"] = status,
            ["transactionStatus"] = transactionStatus,
            ["txnRef"] = txnRef
        };

        var separator = clientReturnUrl.Contains('?', StringComparison.Ordinal) ? "&" : "?";

        return $"{clientReturnUrl}{separator}{BuildData(redirectParameters)}";
    }

    private static string BuildData(IReadOnlyDictionary<string, string> parameters)
    {
        return string.Join("&", parameters.Select(parameter =>
            $"{WebUtility.UrlEncode(parameter.Key)}={WebUtility.UrlEncode(parameter.Value)}"));
    }

    private static string GetParameter(
        IReadOnlyDictionary<string, string?> parameters,
        string key)
    {
        return parameters.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static string ComputeHmacSha512(string data, string secret)
    {
        var keyBytes = Encoding.UTF8.GetBytes(secret);
        var dataBytes = Encoding.UTF8.GetBytes(data);

        using var hmac = new HMACSHA512(keyBytes);
        var hashBytes = hmac.ComputeHash(dataBytes);

        return Convert.ToHexString(hashBytes).ToLowerInvariant();
    }

    private void EnsureConfigured()
    {
        if (string.IsNullOrWhiteSpace(_options.PaymentUrl) ||
            string.IsNullOrWhiteSpace(_options.ReturnUrl) ||
            string.IsNullOrWhiteSpace(_options.TmnCode) ||
            string.IsNullOrWhiteSpace(_options.HashSecret))
        {
            throw new InvalidOperationException("VNPAY configuration is incomplete.");
        }
    }
}
