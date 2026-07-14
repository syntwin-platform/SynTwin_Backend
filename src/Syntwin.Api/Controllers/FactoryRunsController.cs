using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.FactoryRuns.Dtos;
using Syntwin.Application.FactoryRuns.Interfaces;
using Syntwin.Application.RobotSafety.Dtos;
using Syntwin.Application.RobotSafety.Exceptions;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/factory-runs")]
public sealed class FactoryRunsController : ControllerBase
{
    private readonly IFactoryRunService _factoryRunService;

    public FactoryRunsController(IFactoryRunService factoryRunService)
    {
        _factoryRunService = factoryRunService;
    }

    [HttpPost]
    [ProducesResponseType(typeof(FactoryRunResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(SafetyValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactoryRunResponse>> Create(
        [FromBody] CreateFactoryRunRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _factoryRunService.CreateAsync(
                userId.Value,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return response is null
                ? NotFound(new { message = "Company not found or access denied." })
                : CreatedAtAction(nameof(GetById), new { id = response.Id }, response);
        }
        catch (RobotSafetyValidationException exception)
        {
            return BadRequest(new SafetyValidationErrorResponse
            {
                Message = exception.Message,
                Diagnostics = exception.Result.Diagnostics
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/prepare")]
    [ProducesResponseType(typeof(FactoryRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SafetyValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactoryRunResponse>> Prepare(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _factoryRunService.PrepareAsync(
                userId.Value,
                id,
                GetClientIpAddress(),
                cancellationToken);

            return response is null
                ? NotFound(new { message = "Factory run not found." })
                : Ok(response);
        }
        catch (RobotSafetyValidationException exception)
        {
            return BadRequest(new SafetyValidationErrorResponse
            {
                Message = exception.Message,
                Diagnostics = exception.Result.Diagnostics
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/start")]
    [ProducesResponseType(typeof(FactoryRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(SafetyValidationErrorResponse), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactoryRunResponse>> Start(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _factoryRunService.StartAsync(
                userId.Value,
                id,
                GetClientIpAddress(),
                cancellationToken);

            return response is null
                ? NotFound(new { message = "Factory run not found." })
                : Ok(response);
        }
        catch (RobotSafetyValidationException exception)
        {
            return BadRequest(new SafetyValidationErrorResponse
            {
                Message = exception.Message,
                Diagnostics = exception.Result.Diagnostics
            });
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpPost("{id:guid}/cancel")]
    [ProducesResponseType(typeof(FactoryRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactoryRunResponse>> Cancel(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        try
        {
            var response = await _factoryRunService.CancelAsync(
                userId.Value,
                id,
                GetClientIpAddress(),
                cancellationToken);

            return response is null
                ? NotFound(new { message = "Factory run not found." })
                : Ok(response);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(
                StatusCodes.Status403Forbidden,
                new { message = exception.Message });
        }
    }

    [HttpGet("{id:guid}")]
    [ProducesResponseType(typeof(FactoryRunResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<FactoryRunResponse>> GetById(
        Guid id,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            return Unauthorized(new { message = "Invalid access token." });
        }

        var response = await _factoryRunService.GetByIdAsync(
            userId.Value,
            id,
            cancellationToken);

        return response is null
            ? NotFound(new { message = "Factory run not found." })
            : Ok(response);
    }

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }

    private string? GetClientIpAddress()
    {
        return HttpContext.Connection.RemoteIpAddress?.ToString();
    }
}