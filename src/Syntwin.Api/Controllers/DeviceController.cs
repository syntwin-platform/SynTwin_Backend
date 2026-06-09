using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/device")]
public sealed class DeviceController : ControllerBase
{
    private readonly IDeviceGatewayService _deviceGatewayService;

    public DeviceController(IDeviceGatewayService deviceGatewayService)
    {
        _deviceGatewayService = deviceGatewayService;
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Heartbeat(
        [FromHeader(Name = "X-Robot-Id")][Required] string robotIdHeader,
        [FromHeader(Name = "X-Device-Secret")][Required] string deviceSecret,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Invalid X-Robot-Id header." });
        }

        var result = await _deviceGatewayService.HeartbeatAsync(
            robotId,
            deviceSecret,
            GetClientIpAddress(),
            cancellationToken);

        return result switch
        {
            true => Ok(new { message = "Heartbeat accepted." }),
            false => StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." }),
            _ => Unauthorized(new { message = "Invalid device credentials." })
        };
    }

    [HttpPost("telemetry")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitTelemetry(
    [FromHeader(Name = "X-Robot-Id")][Required] string robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")][Required] string deviceSecret,
    [FromBody] DeviceTelemetryRequest request,
    CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Invalid X-Robot-Id header." });
        }

        try
        {
            var result = await _deviceGatewayService.SubmitTelemetryAsync(
                robotId,
                deviceSecret,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return result switch
            {
                true => Ok(new { message = "Telemetry accepted." }),
                false => StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." }),
                _ => Unauthorized(new { message = "Invalid device credentials." })
            };
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet("commands/pending")]
    [ProducesResponseType(typeof(DeviceCommandPendingResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> GetPendingCommand(
        [FromHeader(Name = "X-Robot-Id")][Required] string robotIdHeader,
        [FromHeader(Name = "X-Device-Secret")][Required] string deviceSecret,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Invalid X-Robot-Id header." });
        }

        var command = await _deviceGatewayService.TakePendingCommandAsync(
            robotId,
            deviceSecret,
            GetClientIpAddress(),
            cancellationToken);

        if (!command.IsAuthenticated)
        {
            return Unauthorized(new { message = "Invalid device credentials." });
        }

        if (command.IsDisabled)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." });
        }

        return command.Command is null ? NoContent() : Ok(command.Command);
    }

    [HttpPost("commands/result")]
    [ProducesResponseType(typeof(DeviceCommandResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitCommandResult(
        [FromHeader(Name = "X-Robot-Id")][Required] string robotIdHeader,
        [FromHeader(Name = "X-Device-Secret")][Required] string deviceSecret,
        [FromBody] DeviceCommandResultRequest request,
        CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Invalid X-Robot-Id header." });
        }

        try
        {
            var commandResult = await _deviceGatewayService.SubmitCommandResultAsync(
                robotId,
                deviceSecret,
                request,
                GetClientIpAddress(),
                cancellationToken);

            if (!commandResult.IsAuthenticated)
            {
                return Unauthorized(new { message = "Invalid device credentials." });
            }

            if (commandResult.IsDisabled)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." });
            }

            return Ok(commandResult.Result);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
