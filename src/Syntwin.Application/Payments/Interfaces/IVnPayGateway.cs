using Syntwin.Application.Payments.Dtos;

namespace Syntwin.Application.Payments.Interfaces;

public interface IVnPayGateway
{
    string CreatePaymentUrl(VnPayPaymentUrlRequest request);

    bool IsValidSignature(IReadOnlyDictionary<string, string?> parameters);

    string CreateReturnRedirectUrl(IReadOnlyDictionary<string, string?> parameters);
}
