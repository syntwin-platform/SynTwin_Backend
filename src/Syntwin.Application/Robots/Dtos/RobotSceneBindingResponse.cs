namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotSceneBindingResponse
{
    public Guid Id { get; set; }

    public Guid RobotId { get; set; }

    public string SceneType { get; set; } = string.Empty;

    public double BaseX { get; set; }

    public double BaseY { get; set; }

    public double BaseZ { get; set; }

    public double BaseYaw { get; set; }

    public string? UrdfPath { get; set; }

    public string? PrimPath { get; set; }

    public string? RosNamespace { get; set; }

    public string? GraphPath { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? UpdatedAt { get; set; }
}