namespace Syntwin.Application.Payments.Dtos;

public sealed class CreateVnPayCheckoutRequest
{
    public string SubscriptionPlan { get; set; } = string.Empty;
}