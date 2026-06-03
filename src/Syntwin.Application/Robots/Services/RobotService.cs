using System.Security.Cryptography;
using Syntwin.Application.AuditLogs.Interfaces;
using Syntwin.Application.Common.Interfaces;
using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Robots.Interfaces;
using Syntwin.Application.Users.Interfaces;
using Syntwin.Domain.Entities;
using Syntwin.Domain.Enums;

namespace Syntwin.Application.Robots.Services;

public sealed class RobotService : IRobotService
{
    private readonly IRobotRepository _robotRepository;
    private readonly IUserRepository _userRepository;
    private readonly IPasswordHasher _passwordHasher;
    private readonly IAuditLogRepository _auditLogRepository;
    private readonly IRobotStateCache _robotStateCache;

    public RobotService(
        IRobotRepository robotRepository,
        IUserRepository userRepository,
        IPasswordHasher passwordHasher,
        IAuditLogRepository auditLogRepository,
        IRobotStateCache robotStateCache)
    {
        _robotRepository = robotRepository;
        _userRepository = userRepository;
        _passwordHasher = passwordHasher;
        _auditLogRepository = auditLogRepository;
        _robotStateCache = robotStateCache;
    }
    public async Task<CreateRobotResponse> CreateAsync(
        Guid userId,
        CreateRobotRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var user = await _userRepository.GetByIdAsync(userId, cancellationToken);

        if (user is null)
        {
            throw new InvalidOperationException("User not found.");
        }

        var plan = user.Subscriptions
            .Where(subscription => subscription.Status == SubscriptionStatus.Active)
            .OrderByDescending(subscription => subscription.StartsAt)
            .Select(subscription => subscription.Plan)
            .FirstOrDefault();

        var maxRobots = plan?.MaxRobots ?? 1;
        var currentRobotCount = await _robotRepository.CountActiveByUserIdAsync(userId, cancellationToken);

        if (currentRobotCount >= maxRobots)
        {
            throw new InvalidOperationException("Robot limit reached for current subscription plan.");
        }

        var deviceSecret = CreateDeviceSecret();

        var robot = new Robot
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            RobotName = request.RobotName.Trim(),
            Model = request.Model.Trim(),
            ConnectionType = string.IsNullOrWhiteSpace(request.ConnectionType)
                ? "HTTP"
                : request.ConnectionType.Trim(),
            Status = RobotStatus.Registered,
            DeviceTokenHash = _passwordHasher.Hash(deviceSecret),
            IpAddress = NormalizeNullable(request.IpAddress),
            Port = request.Port,
            CreatedAt = DateTimeOffset.UtcNow
        };

        await _robotRepository.AddAsync(robot, cancellationToken);
        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robot.Id,
            Action = "ROBOT_CREATED",
            IpAddress = NormalizeNullable(ipAddress),
            Message = $"Robot '{robot.RobotName}' was created.",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _robotRepository.SaveChangesAsync(cancellationToken);

        return new CreateRobotResponse
        {
            Robot = ToResponse(robot),
            DeviceSecret = deviceSecret
        };
    }

    public async Task<IReadOnlyList<RobotResponse>> GetMineAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var robots = await _robotRepository.ListByUserIdAsync(userId, cancellationToken);
        return robots.Select(ToResponse).ToList();
    }

    public async Task<RobotResponse?> GetByIdAsync(
        Guid userId,
        Guid robotId,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        return ToResponse(robot);
    }
    public async Task<RobotLatestStateResponse?> GetLatestStateAsync(
    Guid userId,
    Guid robotId,
    CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        var isOnline = await _robotStateCache.IsOnlineAsync(robotId, cancellationToken);
        var redisLastSeenAt = await _robotStateCache.GetLastSeenAsync(robotId, cancellationToken);
        var latest = await _robotStateCache.GetLatestAsync(robotId, cancellationToken);

        if (latest is not null)
        {
            latest.IsOnline = isOnline;
            latest.Status = isOnline ? RobotStatus.Online.ToString() : latest.Status;
            latest.LastSeenAt = redisLastSeenAt ?? latest.LastSeenAt;
            latest.Source = "Redis";

            return latest;
        }

        return new RobotLatestStateResponse
        {
            RobotId = robot.Id,
            IsOnline = isOnline,
            Status = isOnline ? RobotStatus.Online.ToString() : robot.Status.ToString(),
            LastSeenAt = redisLastSeenAt ?? robot.LastSeenAt,
            Source = "SqlFallback"
        };
    }
    public async Task<RobotResponse?> UpdateAsync(
        Guid userId,
        Guid robotId,
        UpdateRobotRequest request,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot be updated.");
        }

        robot.RobotName = request.RobotName.Trim();
        robot.Model = request.Model.Trim();
        robot.ConnectionType = string.IsNullOrWhiteSpace(request.ConnectionType)
            ? "HTTP"
            : request.ConnectionType.Trim();
        robot.IpAddress = NormalizeNullable(request.IpAddress);
        robot.Port = request.Port;
        robot.UpdatedAt = DateTimeOffset.UtcNow;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robot.Id,
            Action = "ROBOT_UPDATED",
            IpAddress = NormalizeNullable(ipAddress),
            Message = $"Robot '{robot.RobotName}' was updated.",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _robotRepository.SaveChangesAsync(cancellationToken);

        return ToResponse(robot);
    }

    public async Task<bool> DisableAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return false;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            return true;
        }

        robot.Status = RobotStatus.Disabled;
        robot.UpdatedAt = DateTimeOffset.UtcNow;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robot.Id,
            Action = "ROBOT_DISABLED",
            IpAddress = NormalizeNullable(ipAddress),
            Message = $"Robot '{robot.RobotName}' was disabled.",
            CreatedAt = DateTimeOffset.UtcNow
        }, cancellationToken);

        await _robotRepository.SaveChangesAsync(cancellationToken);

        return true;
    }

    public async Task<ResetRobotDeviceSecretResponse?> ResetDeviceSecretAsync(
        Guid userId,
        Guid robotId,
        string? ipAddress,
        CancellationToken cancellationToken = default)
    {
        var robot = await _robotRepository.GetByIdAsync(robotId, cancellationToken);

        if (robot is null || robot.UserId != userId)
        {
            return null;
        }

        if (robot.Status == RobotStatus.Disabled)
        {
            throw new InvalidOperationException("Disabled robot cannot reset device secret.");
        }

        var deviceSecret = CreateDeviceSecret();
        var now = DateTimeOffset.UtcNow;

        robot.DeviceTokenHash = _passwordHasher.Hash(deviceSecret);
        robot.UpdatedAt = now;

        await _auditLogRepository.AddAsync(new AuditLog
        {
            UserId = userId,
            RobotId = robot.Id,
            Action = "DEVICE_SECRET_RESET",
            IpAddress = NormalizeNullable(ipAddress),
            Message = $"Device secret for robot '{robot.RobotName}' was reset.",
            CreatedAt = now
        }, cancellationToken);

        await _robotRepository.SaveChangesAsync(cancellationToken);

        return new ResetRobotDeviceSecretResponse
        {
            RobotId = robot.Id,
            DeviceSecret = deviceSecret,
            UpdatedAt = now
        };
    }

    private static RobotResponse ToResponse(Robot robot)
    {
        return new RobotResponse
        {
            Id = robot.Id,
            UserId = robot.UserId,
            RobotName = robot.RobotName,
            Model = robot.Model,
            ConnectionType = robot.ConnectionType,
            Status = robot.Status.ToString(),
            LastSeenAt = robot.LastSeenAt,
            IpAddress = robot.IpAddress,
            Port = robot.Port,
            CreatedAt = robot.CreatedAt,
            UpdatedAt = robot.UpdatedAt
        };
    }

    private static string CreateDeviceSecret()
    {
        return $"sk_robot_{RandomNumberGenerator.GetHexString(32).ToLowerInvariant()}";
    }

    private static string? NormalizeNullable(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}
