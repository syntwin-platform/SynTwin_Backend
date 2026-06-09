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

    public TelemetryHub(IRobotRepository robotRepository)
    {
        _robotRepository = robotRepository;
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

        if (robot is null || robot.UserId != userId.Value)
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