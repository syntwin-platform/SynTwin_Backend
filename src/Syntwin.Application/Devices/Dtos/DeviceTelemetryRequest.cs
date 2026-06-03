using System.ComponentModel.DataAnnotations;
using Syntwin.Application.Robots.Dtos;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceTelemetryRequest
{
    [Required]
    public Guid RobotId { get; set; }

    [Required]
    public TcpPoseDto TcpPose { get; set; } = new();

    [Required]
    [MinLength(6)]
    [MaxLength(6)]
    public IReadOnlyList<double> JointAngles { get; set; } = Array.Empty<double>();

    public double? Temperature { get; set; }

    [Required]
    [MaxLength(50)]
    public string StatusCode { get; set; } = string.Empty;

    public bool CollisionWarning { get; set; }

    public DateTimeOffset? Timestamp { get; set; }
}