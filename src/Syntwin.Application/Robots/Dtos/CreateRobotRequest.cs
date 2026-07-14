using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Robots.Dtos;

public sealed class CreateRobotRequest
{
    public Guid CompanyId { get; set; }

    public Guid? RobotModelId { get; set; }

    [Required]
    [MaxLength(100)]
    public string RobotName { get; set; } = string.Empty;

    [Required]
    [MaxLength(50)]
    public string Model { get; set; } = string.Empty;

    [MaxLength(50)]
    public string ConnectionType { get; set; } = "HTTP";

    [MaxLength(45)]
    public string? IpAddress { get; set; }

    [Range(1, 65535)]
    public int? Port { get; set; }

    public RobotSceneBindingRequest? SceneBinding { get; set; }
}
