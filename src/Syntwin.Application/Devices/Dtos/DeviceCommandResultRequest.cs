using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceCommandResultRequest
{
    [Required]
    public Guid CommandId { get; set; }

    [Required]
    public Guid RobotId { get; set; }

    public bool Success { get; set; }

    public string Status { get; set; } = "Completed";

    [MaxLength(500)]
    public string? Message { get; set; }

    public JsonElement? RawPayload { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }
}