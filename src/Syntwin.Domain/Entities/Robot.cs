using Syntwin.Domain.Enums;

namespace Syntwin.Domain.Entities;

public sealed class Robot
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid UserId { get; set; }

    public Guid CompanyId { get; set; }

    public string RobotName { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ConnectionType { get; set; } = "HTTP";

    public RobotStatus Status { get; set; } = RobotStatus.Registered;

    public string? DeviceTokenHash { get; set; }

    public DateTimeOffset? LastSeenAt { get; set; }

    public string? IpAddress { get; set; }

    public int? Port { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public User? User { get; set; }

    public Company? Company { get; set; }

    public ICollection<RobotCommand> Commands { get; set; } = new List<RobotCommand>();

    public ICollection<CommandResult> CommandResults { get; set; } = new List<CommandResult>();

    public ICollection<AuditLog> AuditLogs { get; set; } = new List<AuditLog>();

    public ICollection<RobotProgram> Programs { get; set; } = new List<RobotProgram>();
    public ICollection<RobotRuntimeSession> RuntimeSessions { get; set; } = new List<RobotRuntimeSession>();

    public ICollection<RobotSafetyPolicy> SafetyPolicies { get; set; } = new List<RobotSafetyPolicy>();
}
