using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Commands.Dtos;
using Syntwin.Application.Commands.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots/{robotId:guid}/commands")]
public sealed class RobotCommandsController : ControllerBase
{
    private readonly IRobotCommandService _commandService;

    public RobotCommandsController(IRobotCommandService commandService)
    {
        _commandService = commandService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(RobotCommandResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotCommandResponse>> Create(
        Guid robotId,
        [FromBody] CreateRobotCommandRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var command = await _commandService.CreateAsync(
                userId.Value,
                robotId,
                request,
                HttpContext.Connection.RemoteIpAddress?.ToString(),
                cancellationToken);

            return command is null
                ? NotFound(new { message = "Robot not found." })
                : CreatedAtAction(nameof(List), new { robotId }, command);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotCommandResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RobotCommandResponse>>> List(
        Guid robotId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        var commands = await _commandService.ListAsync(userId.Value, robotId, cancellationToken);

        return commands is null
            ? NotFound(new { message = "Robot not found." })
            : Ok(commands);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
