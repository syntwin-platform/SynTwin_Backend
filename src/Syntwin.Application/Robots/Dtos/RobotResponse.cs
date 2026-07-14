namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotResponse
{
    public Guid Id { get; set; }

    public Guid UserId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid? RobotModelId { get; set; }

    public string CurrentUserRole { get; set; } = string.Empty;

    public string RobotName { get; set; } = string.Empty;

    public string Model { get; set; } = string.Empty;

    public string ConnectionType { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public DateTimeOffset? LastSeenAt { get; set; }

    public string? IpAddress { get; set; }

    public int? Port { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }

    public RobotSceneBindingResponse? SceneBinding { get; set; }
}
