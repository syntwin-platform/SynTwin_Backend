using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Devices.Dtos;
using Syntwin.Application.Devices.Interfaces;
using Microsoft.Extensions.Options;
using Syntwin.Application.Robots.Options;

namespace Syntwin.Api.Controllers;

[ApiController]
[AllowAnonymous]
[Route("api/device")]
public sealed class DeviceController : ControllerBase
{
    private readonly IDeviceGatewayService _deviceGatewayService;
    private readonly bool _allowLegacyDeviceSecretAuth;

    public DeviceController(
    IDeviceGatewayService deviceGatewayService,
    IOptions<RobotRuntimeOptions> options)
    {
        _deviceGatewayService = deviceGatewayService;
        _allowLegacyDeviceSecretAuth = options.Value.AllowLegacyDeviceSecretAuth;
    }

    [HttpPost("session")]
    [ProducesResponseType(typeof(DeviceSessionResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> CreateSession(
    [FromHeader(Name = "X-Robot-Id")][Required] string robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")][Required] string deviceSecret,
    CancellationToken cancellationToken)
    {
        if (!Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Invalid X-Robot-Id header." });
        }

        var session = await _deviceGatewayService.CreateSessionAsync(
            robotId,
            deviceSecret,
            GetClientIpAddress(),
            cancellationToken);

        if (session is null)
        {
            return Unauthorized(new { message = "Invalid device credentials or robot is disabled." });
        }

        return Ok(session);
    }

    [HttpPost("heartbeat")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Heartbeat(
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromHeader(Name = "X-Robot-Id")] string? robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")] string? deviceSecret,
    CancellationToken cancellationToken)
    {
        if (TryReadBearerToken(authorization, out var accessToken))
        {
            var sessionResult = await _deviceGatewayService.HeartbeatWithSessionAsync(
                accessToken,
                GetClientIpAddress(),
                cancellationToken);

            return sessionResult switch
            {
                true => Ok(new { message = "Heartbeat accepted." }),
                false => StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." }),
                _ => Unauthorized(new { message = "Invalid device session." })
            };
        }

        if (!_allowLegacyDeviceSecretAuth)
        {
            return Unauthorized(new
            {
                message = "Device session token is required. Create a session with POST /api/device/session."
            });
        }

        if (string.IsNullOrWhiteSpace(robotIdHeader) ||
            string.IsNullOrWhiteSpace(deviceSecret) ||
            !Guid.TryParse(robotIdHeader, out var robotId))
        {
            return Unauthorized(new { message = "Missing or invalid device credentials." });
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
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromHeader(Name = "X-Robot-Id")] string? robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")] string? deviceSecret,
    [FromBody] DeviceTelemetryRequest request,
    CancellationToken cancellationToken)
    {
        try
        {
            if (TryReadBearerToken(authorization, out var accessToken))
            {
                var sessionResult = await _deviceGatewayService.SubmitTelemetryWithSessionAsync(
                    accessToken,
                    request,
                    GetClientIpAddress(),
                    cancellationToken);

                return sessionResult switch
                {
                    true => Ok(new { message = "Telemetry accepted." }),
                    false => StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." }),
                    _ => Unauthorized(new { message = "Invalid device session." })
                };
            }

            if (!_allowLegacyDeviceSecretAuth)
            {
                return Unauthorized(new
                {
                    message = "Device session token is required. Create a session with POST /api/device/session."
                });
            }

            if (string.IsNullOrWhiteSpace(robotIdHeader) ||
                string.IsNullOrWhiteSpace(deviceSecret) ||
                !Guid.TryParse(robotIdHeader, out var robotId))
            {
                return Unauthorized(new { message = "Missing or invalid device credentials." });
            }

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
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromHeader(Name = "X-Robot-Id")] string? robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")] string? deviceSecret,
    [FromQuery] bool isBusy,
    [FromQuery] int waitSeconds,
    CancellationToken cancellationToken)
    {
        try
        {
            DevicePendingCommandResult command;

            if (TryReadBearerToken(authorization, out var accessToken))
            {
                command = await _deviceGatewayService.TakePendingCommandWithSessionAsync(
                    accessToken,
                    isBusy,
                    waitSeconds,
                    GetClientIpAddress(),
                    cancellationToken);
            }
            else
            {
                if (!_allowLegacyDeviceSecretAuth)
                {
                    return Unauthorized(new
                    {
                        message =   
                            "Device session token is required. " +
                            "Create a session with POST /api/device/session."
                    });
                }

                if (string.IsNullOrWhiteSpace(robotIdHeader) ||
                    string.IsNullOrWhiteSpace(deviceSecret) ||
                    !Guid.TryParse(robotIdHeader, out var robotId))
                {
                    return Unauthorized(new
                    {
                        message = "Missing or invalid device credentials."
                    });
                }

                command = await _deviceGatewayService.TakePendingCommandAsync(
                    robotId,
                    deviceSecret,
                    isBusy,
                    waitSeconds,
                    GetClientIpAddress(),
                    cancellationToken);
            }

            if (!command.IsAuthenticated)
            {
                return Unauthorized(new
                {
                    message = "Invalid device session or credentials."
                });
            }

            if (command.IsDisabled)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Robot is disabled." });
            }

            return command.Command is null
                ? NoContent()
                : Ok(command.Command);
        }
        catch (OperationCanceledException)
            when (cancellationToken.IsCancellationRequested)
        {
            // Disconnecting the simulator intentionally cancels its long-poll request.
            return NoContent();
        }
    }

    [HttpPost("factory-runs/armed")]
    [ProducesResponseType(typeof(DeviceFactoryRunArmResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ArmFactoryRun(
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromBody] DeviceFactoryRunArmRequest request,
    CancellationToken cancellationToken)
    {
        try
        {
            if (!TryReadBearerToken(authorization, out var accessToken))
            {
                return Unauthorized(new
                {
                    message = "Device session token is required. Create a session with POST /api/device/session."
                });
            }

            var result = await _deviceGatewayService.ArmFactoryRunCommandWithSessionAsync(
                accessToken,
                request,
                GetClientIpAddress(),
                cancellationToken);

            if (!result.IsAuthenticated)
            {
                return Unauthorized(new { message = "Invalid device session." });
            }

            if (result.IsDisabled)
            {
                return StatusCode(StatusCodes.Status403Forbidden, new { message = "Robot is disabled." });
            }

            return Ok(result.Response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost("factory-runs/started")]
    [ProducesResponseType(
    typeof(DeviceFactoryRunStartedResponse),
    StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ReportFactoryRunStarted(
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromBody] DeviceFactoryRunStartedRequest request,
    CancellationToken cancellationToken)
    {
        try
        {
            if (!TryReadBearerToken(authorization, out var accessToken))
            {
                return Unauthorized(new
                {
                    message =
                        "Device session token is required. " +
                        "Create a session with POST /api/device/session."
                });
            }

            var result =
                await _deviceGatewayService
                    .ReportFactoryRunStartedWithSessionAsync(
                        accessToken,
                        request,
                        GetClientIpAddress(),
                        cancellationToken);

            if (!result.IsAuthenticated)
            {
                return Unauthorized(new
                {
                    message = "Invalid device session."
                });
            }

            if (result.IsDisabled)
            {
                return StatusCode(
                    StatusCodes.Status403Forbidden,
                    new { message = "Robot is disabled." });
            }

            return Ok(result.Response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }


    [HttpPost("commands/result")]
    [ProducesResponseType(typeof(DeviceCommandResultResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SubmitCommandResult(
    [FromHeader(Name = "Authorization")] string? authorization,
    [FromHeader(Name = "X-Robot-Id")] string? robotIdHeader,
    [FromHeader(Name = "X-Device-Secret")] string? deviceSecret,
    [FromBody] DeviceCommandResultRequest request,
    CancellationToken cancellationToken)
    {
        try
        {
            DeviceCommandResultSubmitResult commandResult;

            if (TryReadBearerToken(authorization, out var accessToken))
            {
                commandResult = await _deviceGatewayService.SubmitCommandResultWithSessionAsync(
                    accessToken,
                    request,
                    GetClientIpAddress(),
                    cancellationToken);
            }
            else
            {
                if (!_allowLegacyDeviceSecretAuth)
                {
                    return Unauthorized(new
                    {
                        message = "Device session token is required. Create a session with POST /api/device/session."
                    });
                }

                if (string.IsNullOrWhiteSpace(robotIdHeader) ||
                    string.IsNullOrWhiteSpace(deviceSecret) ||
                    !Guid.TryParse(robotIdHeader, out var robotId))
                {
                    return Unauthorized(new { message = "Missing or invalid device credentials." });
                }

                commandResult = await _deviceGatewayService.SubmitCommandResultAsync(
                    robotId,
                    deviceSecret,
                    request,
                    GetClientIpAddress(),
                    cancellationToken);
            }

            if (!commandResult.IsAuthenticated)
            {
                return Unauthorized(new { message = "Invalid device session or credentials." });
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

    private static bool TryReadBearerToken(
    string? authorization,
    out string accessToken)
    {
        accessToken = string.Empty;

        const string bearerPrefix = "Bearer ";

        if (string.IsNullOrWhiteSpace(authorization) ||
            !authorization.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        accessToken = authorization[bearerPrefix.Length..].Trim();
        return !string.IsNullOrWhiteSpace(accessToken);
    }
    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}
