using Syntwin.Application.Payments.Dtos;

namespace Syntwin.Application.Payments.Interfaces;

public interface IPaymentService
{
    Task<CreateVnPayCheckoutResponse> CreateVnPayCheckoutAsync(
        Guid userId,
        CreateVnPayCheckoutRequest request,
        string ipAddress,
        CancellationToken cancellationToken = default);

    Task<VnPayIpnResponse> ProcessVnPayIpnAsync(
    IReadOnlyDictionary<string, string?> parameters,
    CancellationToken cancellationToken = default);

    Task<VnPayIpnResponse> ProcessVnPayReturnAsync(
    IReadOnlyDictionary<string, string?> parameters,
    CancellationToken cancellationToken = default);

    string CreateVnPayReturnRedirectUrl(
    IReadOnlyDictionary<string, string?> parameters);

    Task<VnPayPaymentStatusResponse?> GetVnPayPaymentStatusAsync(
    Guid userId,
    string merchantTransactionRef,
    CancellationToken cancellationToken = default);
}
