using Syntwin.Application.Payments.Dtos;
using Syntwin.Application.Payments.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;
using System.Globalization;
using System.Text.Json;
namespace Syntwin.Application.Payments.Services;

public sealed class PaymentService : IPaymentService
{
    private readonly IUserRepository _userRepository;
    private readonly IVnPayGateway _vnPayGateway;

    public PaymentService(
        IUserRepository userRepository,
        IVnPayGateway vnPayGateway)
    {
        _userRepository = userRepository;
        _vnPayGateway = vnPayGateway;
    }

    public async Task<CreateVnPayCheckoutResponse> CreateVnPayCheckoutAsync(
        Guid userId,
        CreateVnPayCheckoutRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        if (!Enum.TryParse<SubscriptionPlanCode>(request.SubscriptionPlan, true, out var planCode) ||
            !Enum.IsDefined(planCode))
        {
            throw new InvalidOperationException("Invalid subscription plan.");
        }

        if (planCode == SubscriptionPlanCode.Free)
        {
            throw new InvalidOperationException("Free plan does not require payment.");
        }

        var plan = await _userRepository.GetSubscriptionPlanByCodeAsync(planCode, cancellationToken);

        if (plan is null)
        {
            throw new InvalidOperationException("Subscription plan is not configured.");
        }

        if (plan.MonthlyPrice <= 0)
        {
            throw new InvalidOperationException("Selected plan does not require payment.");
        }

        var now = DateTimeOffset.UtcNow;
        var merchantTransactionRef = CreateMerchantTransactionRef(now);

        var subscription = await _userRepository.GetPendingPaymentSubscriptionAsync(
            userId,
            plan.Id,
            cancellationToken);

        if (subscription is null)
        {
            subscription = new UserSubscription
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                PlanId = plan.Id,
                Status = SubscriptionStatus.PendingPayment,
                StartsAt = now,
                EndsAt = now.AddMinutes(15),
                AutoRenew = false,
                CreatedAt = now
            };

            await _userRepository.AddUserSubscriptionAsync(subscription, cancellationToken);
        }
        else
        {
            subscription.UpdatedAt = now;
            subscription.EndsAt = now.AddMinutes(15);
        }

        var payment = new PaymentTransaction
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            SubscriptionId = subscription.Id,
            Provider = PaymentProvider.VnPay,
            MerchantTransactionRef = merchantTransactionRef,
            Amount = plan.MonthlyPrice,
            Currency = "VND",
            Status = PaymentStatus.Pending,
            CreatedAt = now
        };

        var paymentUrl = _vnPayGateway.CreatePaymentUrl(new VnPayPaymentUrlRequest
        {
            MerchantTransactionRef = merchantTransactionRef,
            Amount = plan.MonthlyPrice,
            OrderInfo = $"SynTwin payment for {plan.Code} plan - {merchantTransactionRef}",
            OrderType = "other",
            IpAddress = string.IsNullOrWhiteSpace(ipAddress) ? "127.0.0.1" : ipAddress,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(15)
        });

        await _userRepository.AddPaymentTransactionAsync(payment, cancellationToken);
        await _userRepository.SaveChangesAsync(cancellationToken);

        return new CreateVnPayCheckoutResponse
        {
            PaymentId = payment.Id,
            MerchantTransactionRef = merchantTransactionRef,
            PaymentUrl = paymentUrl,
            Amount = payment.Amount,
            Currency = payment.Currency,
            Status = payment.Status.ToString()
        };
    }
    public async Task<VnPayIpnResponse> ProcessVnPayIpnAsync(
    IReadOnlyDictionary<string, string?> parameters,
    CancellationToken cancellationToken = default)
    {
        return await ProcessVnPayCallbackAsync(parameters, cancellationToken);
    }

    public async Task<VnPayIpnResponse> ProcessVnPayReturnAsync(
    IReadOnlyDictionary<string, string?> parameters,
    CancellationToken cancellationToken = default)
    {
        return await ProcessVnPayCallbackAsync(parameters, cancellationToken);
    }

    private async Task<VnPayIpnResponse> ProcessVnPayCallbackAsync(
    IReadOnlyDictionary<string, string?> parameters,
    CancellationToken cancellationToken = default)
    {
        bool signatureValid;

        try
        {
            signatureValid = _vnPayGateway.IsValidSignature(parameters);
        }
        catch (InvalidOperationException)
        {
            return new VnPayIpnResponse
            {
                RspCode = "99",
                Message = "VNPAY configuration is incomplete"
            };
        }

        if (!signatureValid)
        {
            return new VnPayIpnResponse
            {
                RspCode = "97",
                Message = "Invalid signature"
            };
        }

        var txnRef = GetRequiredParameter(parameters, "vnp_TxnRef");

        if (string.IsNullOrWhiteSpace(txnRef))
        {
            return new VnPayIpnResponse
            {
                RspCode = "01",
                Message = "Order not found"
            };
        }

        var payment = await _userRepository.GetPaymentTransactionByMerchantRefAsync(
            txnRef,
            cancellationToken);

        if (payment is null)
        {
            return new VnPayIpnResponse
            {
                RspCode = "01",
                Message = "Order not found"
            };
        }

        if (payment.Status != PaymentStatus.Pending)
        {
            if (payment.Status == PaymentStatus.Paid)
            {
                return new VnPayIpnResponse
                {
                    RspCode = "00",
                    Message = "Confirm success"
                };
            }

            return new VnPayIpnResponse
            {
                RspCode = "02",
                Message = "Order already confirmed"
            };
        }

        var vnpAmountRaw = GetRequiredParameter(parameters, "vnp_Amount");

        if (!long.TryParse(vnpAmountRaw, NumberStyles.None, CultureInfo.InvariantCulture, out var vnpAmount))
        {
            return new VnPayIpnResponse
            {
                RspCode = "04",
                Message = "Invalid amount"
            };
        }

        var expectedAmount = decimal.ToInt64(
            decimal.Round(payment.Amount, 0, MidpointRounding.AwayFromZero)) * 100;

        if (vnpAmount != expectedAmount)
        {
            return new VnPayIpnResponse
            {
                RspCode = "04",
                Message = "Invalid amount"
            };
        }

        var now = DateTimeOffset.UtcNow;
        var responseCode = GetRequiredParameter(parameters, "vnp_ResponseCode");
        var transactionStatus = GetRequiredParameter(parameters, "vnp_TransactionStatus");
        var transactionNo = GetRequiredParameter(parameters, "vnp_TransactionNo");
        var bankCode = GetOptionalParameter(parameters, "vnp_BankCode");
        var payDate = ParseVnPayDate(GetOptionalParameter(parameters, "vnp_PayDate"));

        payment.ProviderTransactionId = transactionNo;
        payment.ResponseCode = responseCode;
        payment.TransactionStatus = transactionStatus;
        payment.BankCode = bankCode;
        payment.PayDate = payDate;
        payment.ProcessedAt = now;
        payment.RawPayloadJson = JsonSerializer.Serialize(parameters);

        if (responseCode == "00" && transactionStatus == "00")
        {
            payment.Status = PaymentStatus.Paid;
            payment.PaidAt = payDate ?? now;

            if (payment.Subscription is null)
            {
                payment.FailureReason = "Subscription not found.";
                payment.Status = PaymentStatus.Failed;

                await _userRepository.SaveChangesAsync(cancellationToken);

                return new VnPayIpnResponse
                {
                    RspCode = "99",
                    Message = "Unknown error"
                };
            }

            var activeSubscription = await _userRepository.GetActiveSubscriptionAsync(
                payment.UserId,
                cancellationToken);

            if (activeSubscription is not null &&
                activeSubscription.Id != payment.Subscription.Id)
            {
                activeSubscription.Status = SubscriptionStatus.Canceled;
                activeSubscription.CanceledAt = now;
                activeSubscription.EndsAt = now;
                activeSubscription.UpdatedAt = now;
            }

            payment.Subscription.Status = SubscriptionStatus.Active;
            payment.Subscription.StartsAt = now;
            payment.Subscription.EndsAt = now.AddMonths(1);
            payment.Subscription.UpdatedAt = now;
        }
        else
        {
            payment.Status = PaymentStatus.Failed;
            payment.FailureReason = $"VNPAY response={responseCode}, transactionStatus={transactionStatus}";

            if (payment.Subscription is not null &&
                payment.Subscription.Status == SubscriptionStatus.PendingPayment)
            {
                payment.Subscription.Status = SubscriptionStatus.Canceled;
                payment.Subscription.CanceledAt = now;
                payment.Subscription.EndsAt = now;
                payment.Subscription.UpdatedAt = now;
            }
        }

        await _userRepository.SaveChangesAsync(cancellationToken);

        return new VnPayIpnResponse
        {
            RspCode = "00",
            Message = "Confirm success"
        };
    }

    public string CreateVnPayReturnRedirectUrl(
        IReadOnlyDictionary<string, string?> parameters)
    {
        return _vnPayGateway.CreateReturnRedirectUrl(parameters);
    }

    private static string? GetOptionalParameter(
        IReadOnlyDictionary<string, string?> parameters,
        string key)
    {
        return parameters.TryGetValue(key, out var value) ? value : null;
    }

    private static string GetRequiredParameter(
        IReadOnlyDictionary<string, string?> parameters,
        string key)
    {
        return parameters.TryGetValue(key, out var value) ? value ?? string.Empty : string.Empty;
    }

    private static DateTimeOffset? ParseVnPayDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (!DateTime.TryParseExact(
                value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out var parsed))
        {
            return null;
        }

        return new DateTimeOffset(parsed, TimeSpan.FromHours(7));
    }

    public async Task<VnPayPaymentStatusResponse?> GetVnPayPaymentStatusAsync(
    Guid userId,
    string merchantTransactionRef,
    CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(merchantTransactionRef))
        {
            return null;
        }

        var payment = await _userRepository.GetPaymentTransactionByMerchantRefAsync(
            merchantTransactionRef.Trim(),
            cancellationToken);

        if (payment is null || payment.UserId != userId)
        {
            return null;
        }

        return new VnPayPaymentStatusResponse
        {
            PaymentId = payment.Id,
            MerchantTransactionRef = payment.MerchantTransactionRef ?? string.Empty,
            PaymentStatus = payment.Status.ToString(),
            SubscriptionStatus = payment.Subscription?.Status.ToString(),
            SubscriptionPlan = payment.Subscription?.Plan?.Code.ToString(),
            Amount = payment.Amount,
            Currency = payment.Currency,
            ResponseCode = payment.ResponseCode,
            TransactionStatus = payment.TransactionStatus,
            CreatedAt = payment.CreatedAt,
            PaidAt = payment.PaidAt,
            ProcessedAt = payment.ProcessedAt
        };
    }

    private static string CreateMerchantTransactionRef(DateTimeOffset now)
    {
        return $"ST{now:yyyyMMddHHmmss}{Guid.NewGuid():N}"[..30];
    }
}
