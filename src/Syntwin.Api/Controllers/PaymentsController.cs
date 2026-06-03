using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Payments.Dtos;
using Syntwin.Application.Payments.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Route("api/payments")]
public sealed class PaymentsController : ControllerBase
{
    private readonly IPaymentService _paymentService;

    public PaymentsController(IPaymentService paymentService)
    {
        _paymentService = paymentService;
    }

    [Authorize]
    [HttpPost("vnpay/checkout")]
    [ProducesResponseType(typeof(CreateVnPayCheckoutResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    public async Task<ActionResult<CreateVnPayCheckoutResponse>> CreateVnPayCheckout(
        CreateVnPayCheckoutRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "127.0.0.1";

        try
        {
            var response = await _paymentService.CreateVnPayCheckoutAsync(
                userId.Value,
                request,
                ipAddress,
                cancellationToken);

            return Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [AllowAnonymous]
    [HttpGet("vnpay/ipn")]
    [ProducesResponseType(typeof(VnPayIpnResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<VnPayIpnResponse>> VnPayIpn(
        CancellationToken cancellationToken)
    {
        var parameters = Request.Query.ToDictionary(
            item => item.Key,
            item => (string?)item.Value.ToString(),
            StringComparer.Ordinal);

        var response = await _paymentService.ProcessVnPayIpnAsync(
            parameters,
            cancellationToken);

        return Ok(response);
    }

    [AllowAnonymous]
    [HttpGet("vnpay/return")]
    public async Task<IActionResult> VnPayReturn(CancellationToken cancellationToken)
    {
        var parameters = Request.Query.ToDictionary(
            item => item.Key,
            item => (string?)item.Value.ToString(),
            StringComparer.Ordinal);

        await _paymentService.ProcessVnPayReturnAsync(parameters, cancellationToken);

        var redirectUrl = _paymentService.CreateVnPayReturnRedirectUrl(parameters);

        return Redirect(redirectUrl);
    }
    [Authorize]
    [HttpGet("vnpay/status/{txnRef}")]
    [ProducesResponseType(typeof(VnPayPaymentStatusResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<VnPayPaymentStatusResponse>> GetVnPayPaymentStatus(
    string txnRef,
    CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var response = await _paymentService.GetVnPayPaymentStatusAsync(
            userId.Value,
            txnRef,
            cancellationToken);

        return response is null
            ? NotFound(new { message = "Payment not found." })
            : Ok(response);
    }
    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
