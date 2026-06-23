using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Domain.Enums;

namespace Syntwin.Api.Hubs;

[Authorize]
public sealed class TelemetryHub : Hub
{
    private readonly IRobotRepository _robotRepository;
    private readonly IRobotAccessService _robotAccessService;
    private readonly IRobotStateCache _robotStateCache;
    private static readonly TimeSpan TelemetryViewerTtl = TimeSpan.FromHours(2);
    public TelemetryHub(
    IRobotRepository robotRepository,
    IRobotAccessService robotAccessService,
    IRobotStateCache robotStateCache)
    {
        _robotRepository = robotRepository;
        _robotAccessService = robotAccessService;
        _robotStateCache = robotStateCache;
    }

    public async Task JoinRobotGroup(string robotId)
    {
        var userId = GetCurrentUserId();

        if (userId is null)
        {
            throw new HubException("Invalid access token.");
        }

        if (!Guid.TryParse(robotId, out var parsedRobotId))
        {
            throw new HubException("Invalid robot id.");
        }

        var robot = await _robotRepository.GetByIdAsync(parsedRobotId);

        if (robot is null)
        {
            throw new HubException("Robot not found or access denied.");
        }

        var role = await _robotAccessService.GetCompanyRoleAsync(
            userId.Value,
            robot.CompanyId);

        if (!role.HasValue)
        {
            throw new HubException("Robot not found or access denied.");
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new HubException("Robot is disabled.");
        }

        await Groups.AddToGroupAsync(
            Context.ConnectionId,
            GetRobotGroupName(parsedRobotId));

        await _robotStateCache.AddTelemetryViewerAsync(
    parsedRobotId,
    Context.ConnectionId,
    TelemetryViewerTtl);
    }

    public async Task LeaveRobotGroup(string robotId)
    {
        if (!Guid.TryParse(robotId, out var parsedRobotId))
        {
            throw new HubException("Invalid robot id.");
        }

        await Groups.RemoveFromGroupAsync(
            Context.ConnectionId,
            GetRobotGroupName(parsedRobotId));

        await _robotStateCache.RemoveTelemetryViewerAsync(
    parsedRobotId,
    Context.ConnectionId);
    }

    public override async Task OnDisconnectedAsync(Exception? exception)
    {
        await _robotStateCache.ClearTelemetryViewerConnectionAsync(
            Context.ConnectionId);

        await base.OnDisconnectedAsync(exception);
    }

    public static string GetRobotGroupName(Guid robotId)
    {
        return $"robot-{robotId}";
    }

    private Guid? GetCurrentUserId()
    {
        var value = Context.User?.FindFirstValue(ClaimTypes.NameIdentifier);
        return Guid.TryParse(value, out var userId) ? userId : null;
    }
}
