using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Syntwin.Application.AdminCompanies.Dtos;
using Syntwin.Application.AdminCompanies.Interfaces;

namespace Syntwin.Api.Controllers;

[ApiController]
[Authorize(Roles = "SuperAdmin")]
[Route("api/admin/companies")]
public sealed class AdminCompaniesController : ControllerBase
{
    private readonly IAdminCompanyService _adminCompanyService;

    public AdminCompaniesController(IAdminCompanyService adminCompanyService)
    {
        _adminCompanyService = adminCompanyService;
    }

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<AdminCompanyResponse>>> List(
        [FromQuery] string? search,
        CancellationToken cancellationToken)
    {
        return Ok(await _adminCompanyService.ListAsync(search, cancellationToken));
    }

    [HttpGet("{companyId:guid}/members")]
    public async Task<ActionResult<IReadOnlyList<AdminCompanyMemberResponse>>> ListMembers(
        Guid companyId,
        CancellationToken cancellationToken)
    {
        var members = await _adminCompanyService.ListMembersAsync(
            companyId,
            cancellationToken);

        return members is null
            ? NotFound(new { message = "Company not found." })
            : Ok(members);
    }

    [HttpPost("{companyId:guid}/monitors")]
    public async Task<ActionResult<AdminCompanyMemberResponse>> AddMonitor(
        Guid companyId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var monitor = await _adminCompanyService.AddMonitorAsync(
                companyId,
                request,
                cancellationToken);

            return monitor is null
                ? NotFound(new { message = "Company not found." })
                : Ok(monitor);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpPut("{companyId:guid}/monitors/{monitorUserId:guid}")]
    public async Task<ActionResult<AdminCompanyMemberResponse>> ReplaceMonitor(
        Guid companyId,
        Guid monitorUserId,
        AdminLinkedAccountRequest request,
        CancellationToken cancellationToken)
    {
        try
        {
            var monitor = await _adminCompanyService.ReplaceMonitorAsync(
                companyId,
                monitorUserId,
                request,
                cancellationToken);

            return monitor is null
                ? NotFound(new { message = "Linked monitor account not found." })
                : Ok(monitor);
        }
        catch (InvalidOperationException exception)
        {
            return BadRequest(new { message = exception.Message });
        }
    }

    [HttpDelete("{companyId:guid}/monitors/{monitorUserId:guid}")]
    public async Task<IActionResult> RemoveMonitor(
        Guid companyId,
        Guid monitorUserId,
        CancellationToken cancellationToken)
    {
        var removed = await _adminCompanyService.RemoveMonitorAsync(
            companyId,
            monitorUserId,
            cancellationToken);

        return removed
            ? NoContent()
            : NotFound(new { message = "Linked monitor account not found." });
    }
}
