using System.ComponentModel.DataAnnotations;

namespace Syntwin.Application.Robots.Dtos;

public sealed class RobotIoStateDto
{
    public IReadOnlyDictionary<int, bool> CabinetDigitalOutputs { get; set; } =
        new Dictionary<int, bool>();

    public IReadOnlyDictionary<int, bool> ToolDigitalOutputs { get; set; } =
        new Dictionary<int, bool>();

    [MaxLength(20)]
    public string? GripperState { get; set; }
}
