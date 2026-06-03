namespace Syntwin.Application.Payments.Dtos;

public sealed class VnPayIpnResponse
{
    public string RspCode { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;
}