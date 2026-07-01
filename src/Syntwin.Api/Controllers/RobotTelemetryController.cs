using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots/{robotId:guid}/telemetry")]
public sealed class RobotTelemetryController : ControllerBase
{
    private static readonly TimeSpan DefaultHistoryRange = TimeSpan.FromHours(1);
    private static readonly TimeSpan MaxHistoryRange = TimeSpan.FromDays(7);

    private readonly IRobotService _robotService;
    private readonly IRobotTelemetryHistoryReader _telemetryHistoryReader;

    public RobotTelemetryController(
        IRobotService robotService,
        IRobotTelemetryHistoryReader telemetryHistoryReader)
    {
        _robotService = robotService;
        _telemetryHistoryReader = telemetryHistoryReader;
    }

    [HttpGet("history")]
    [ProducesResponseType(typeof(IReadOnlyList<RobotTelemetryHistoryPoint>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RobotTelemetryHistoryPoint>>> GetHistory(
        Guid robotId,
        [FromQuery] DateTimeOffset? from,
        [FromQuery] DateTimeOffset? to,
        [FromQuery] int? intervalSeconds,
        [FromQuery] int? limit,
        [FromQuery] string[]? fields,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var robot = await _robotService.GetByIdAsync(
            userId.Value,
            robotId,
            cancellationToken);

        if (robot is null)
        {
            return NotFound(new { message = "Robot not found." });
        }

        var now = DateTimeOffset.UtcNow;
        var rangeTo = to?.ToUniversalTime() ?? now;
        var rangeFrom = from?.ToUniversalTime() ?? rangeTo.Subtract(DefaultHistoryRange);

        if (rangeTo <= rangeFrom)
        {
            return BadRequest(new { message = "Query parameter 'to' must be later than 'from'." });
        }

        if (rangeTo - rangeFrom > MaxHistoryRange)
        {
            return BadRequest(new { message = "Telemetry history range cannot exceed 7 days." });
        }

        var query = new RobotTelemetryHistoryQuery
        {
            RobotId = robotId,
            From = rangeFrom,
            To = rangeTo,
            Interval = intervalSeconds.HasValue && intervalSeconds.Value > 0
                ? TimeSpan.FromSeconds(Math.Clamp(intervalSeconds.Value, 1, 86400))
                : null,
            Limit = Math.Clamp(limit ?? 1000, 1, 10000),
            Fields = fields ?? Array.Empty<string>()
        };

        var points = await _telemetryHistoryReader.QueryAsync(
            query,
            cancellationToken);

        return Ok(points);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}