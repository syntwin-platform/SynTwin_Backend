using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotSceneBindingRequest
{
    [MaxLength(50)]
    public string SceneType { get; set; } = "FairinoStudio";

    public double BaseX { get; set; }

    public double BaseY { get; set; }

    public double BaseZ { get; set; }

    public double BaseYaw { get; set; }

    [MaxLength(500)]
    public string? UrdfPath { get; set; }

    [MaxLength(500)]
    public string? PrimPath { get; set; }

    [MaxLength(200)]
    public string? RosNamespace { get; set; }

    [MaxLength(500)]
    public string? GraphPath { get; set; }
}