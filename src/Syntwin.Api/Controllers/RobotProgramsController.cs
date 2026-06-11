using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.RobotPrograms.Dtos;
using Syntwin.Application.RobotPrograms.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots/{robotId:guid}/programs")]
public sealed class RobotProgramsController : ControllerBase
{
    private readonly IRobotProgramService _programService;

    public RobotProgramsController(IRobotProgramService programService)
    {
        _programService = programService;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<RobotProgramResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<IReadOnlyList<RobotProgramResponse>>> List(
        Guid robotId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        var programs = await _programService.ListAsync(
            userId.Value,
            robotId,
            cancellationToken);

        return programs is null
            ? NotFound(new { message = "Robot not found." })
            : Ok(programs);
    }

    [HttpGet("{programId:guid}")]
    [ProducesResponseType(typeof(RobotProgramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotProgramResponse>> GetById(
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        var program = await _programService.GetByIdAsync(
            userId.Value,
            robotId,
            programId,
            cancellationToken);

        return program is null
            ? NotFound(new { message = "Robot program not found." })
            : Ok(program);
    }

    [HttpPost]
    [ProducesResponseType(typeof(RobotProgramResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotProgramResponse>> Create(
        Guid robotId,
        [FromBody] CreateRobotProgramRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var program = await _programService.CreateAsync(
     userId.Value,
     robotId,
     request,
     GetClientIpAddress(),
     cancellationToken);

            return program is null
                ? NotFound(new { message = "Robot not found." })
                : CreatedAtAction(
                    nameof(GetById),
                    new { robotId, programId = program.Id },
                    program);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpPut("{programId:guid}")]
    [ProducesResponseType(typeof(RobotProgramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotProgramResponse>> Update(
        Guid robotId,
        Guid programId,
        [FromBody] UpdateRobotProgramRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var program = await _programService.UpdateAsync(
        userId.Value,
        robotId,
        programId,
        request,
        GetClientIpAddress(),
        cancellationToken);
            return program is null
                ? NotFound(new { message = "Robot program not found." })
                : Ok(program);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpPost("{programId:guid}/publish")]
    [ProducesResponseType(typeof(RobotProgramResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotProgramResponse>> Publish(
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var program = await _programService.PublishAsync(
     userId.Value,
     robotId,
     programId,
     GetClientIpAddress(),
     cancellationToken);
            return program is null
                ? NotFound(new { message = "Robot program not found." })
                : Ok(program);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpDelete("{programId:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Archive(
        Guid robotId,
        Guid programId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var archived = await _programService.ArchiveAsync(
        userId.Value,
        robotId,
        programId,
        GetClientIpAddress(),
        cancellationToken);

            return archived
                ? NoContent()
                : NotFound(new { message = "Robot program not found." });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
