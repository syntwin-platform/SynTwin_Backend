using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.Companies.Dtos;
using Syntwin.Application.Companies.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/companies")]
public sealed class CompaniesController : ControllerBase
{
    private readonly ICompanyService _companyService;

    public CompaniesController(ICompanyService companyService)
    {
        _companyService = companyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<CompanyResponse>>> ListMine(
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        return Ok(await _companyService.ListMineAsync(userId.Value, cancellationToken));
    }

    [HttpGet("{companyId:guid}")]
    public async Task<ActionResult<CompanyResponse>> Get(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        var company = await _companyService.GetAsync(
            userId.Value,
            companyId,
            cancellationToken);

        return company is null
            ? NotFound(new { message = "Company not found." })
            : Ok(company);
    }

    [HttpPost]
    public async Task<ActionResult<CompanyResponse>> Create(
        CreateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var company = await _companyService.CreateAsync(
                userId.Value,
                request,
                cancellationToken);

            return CreatedAtAction(
                nameof(Get),
                new { companyId = company.Id },
                company);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{companyId:guid}")]
    public async Task<ActionResult<CompanyResponse>> Update(
        Guid companyId,
        UpdateCompanyRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var company = await _companyService.UpdateAsync(
                userId.Value,
                companyId,
                request,
                cancellationToken);

            return company is null
                ? NotFound(new { message = "Company not found." })
                : Ok(company);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpGet("{companyId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<CompanyMemberResponse>>> ListMembers(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized(new { message = "Invalid access token." });

        try
        {
            var members = await _companyService.ListMembersAsync(
                userId.Value,
                companyId,
                cancellationToken);

            return members is null
                ? NotFound(new { message = "Company not found." })
                : Ok(members);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = exception.Message });
        }
    }

    [HttpPost("{companyId:guid}/monitors")]
    public async Task<ActionResult<CompanyMemberResponse>> AddMonitor(
        Guid companyId,
        OwnerLinkedAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _companyService.AddMonitorAsync(
                userId.Value,
                companyId,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPut("{companyId:guid}/monitors/{monitorUserId:guid}")]
    public async Task<ActionResult<CompanyMemberResponse>> ReplaceMonitor(
        Guid companyId,
        Guid monitorUserId,
        OwnerLinkedAccountRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _companyService.ReplaceMonitorAsync(
                userId.Value,
                companyId,
                monitorUserId,
                request,
                GetClientIpAddress(),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpDelete("{companyId:guid}/monitors/{monitorUserId:guid}")]
    public async Task<IActionResult> RemoveMonitor(
        Guid companyId,
        Guid monitorUserId,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var removed = await _companyService.RemoveMonitorAsync(
                userId.Value,
                companyId,
                monitorUserId,
                GetClientIpAddress(),
                cancellationToken);

            return removed ? NoContent() : NotFound();
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(403, new { message = ex.Message });
        }
    }

    [HttpPatch("{companyId:guid}/monitors/{monitorUserId:guid}/status")]
    public async Task<ActionResult<CompanyMemberResponse>> SetMonitorStatus(
        Guid companyId,
        Guid monitorUserId,
        UpdateMonitorStatusRequest request,
        CancellationToken cancellationToken)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Unauthorized();

        try
        {
            var result = await _companyService.SetMonitorStatusAsync(
                userId.Value,
                companyId,
                monitorUserId,
                request.IsActive,
                GetClientIpAddress(),
                cancellationToken);

            return result is null ? NotFound() : Ok(result);
        }
        catch (UnauthorizedAccessException exception)
        {
            return StatusCode(403, new { message = exception.Message });
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

    private Guid? GetCurrentUserId()
    {
        var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
