using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.LuaParsing.Dtos;
using Syntwin.Application.LuaParsing.Interfaces;
using Syntwin.Application.RobotPrograms.Dtos;
namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/robots/{robotId:guid}/programs/import/lua")]
public sealed class RobotProgramLuaImportsController : ControllerBase
{
    private readonly ILuaProgramImportService _luaProgramImportService;

    public RobotProgramLuaImportsController(
        ILuaProgramImportService luaProgramImportService)
    {
        _luaProgramImportService = luaProgramImportService;
    }

    [HttpPost("preview")]
    [ProducesResponseType(typeof(LuaParsePreviewResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LuaParsePreviewResponse>> Preview(
        Guid robotId,
        [FromBody] LuaParseRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var preview = await _luaProgramImportService.PreviewAsync(
                userId.Value,
                robotId,
                request,
                cancellationToken);

            return preview is null
                ? NotFound(new { message = "Robot not found." })
                : Ok(preview);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPost]
    [ProducesResponseType(typeof(RobotProgramResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<RobotProgramResponse>> Import(
    Guid robotId,
    [FromBody] LuaParseRequest request,
    CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var program = await _luaProgramImportService.ImportAsync(
                userId.Value,
                robotId,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return program is null
                ? NotFound(new { message = "Robot not found." })
                : CreatedAtAction(
                    "GetById",
                    "RobotPrograms",
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