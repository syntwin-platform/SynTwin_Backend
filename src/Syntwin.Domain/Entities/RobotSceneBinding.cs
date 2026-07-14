namespace Syntwin.Domain.Entities;

public sealed class RobotSceneBinding
{
    public Guid Id { get; set; } = Guid.NewGuid();

    public Guid RobotId { get; set; }

    public string SceneType { get; set; } = "FairinoStudio";

    public double BaseX { get; set; }

    public double BaseY { get; set; }

    public double BaseZ { get; set; }

    public double BaseYaw { get; set; }

    public string? UrdfPath { get; set; }

    public string? PrimPath { get; set; }

    public string? RosNamespace { get; set; }

    public string? GraphPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? UpdatedAt { get; set; }

    public Robot Robot { get; set; } = null!;
}