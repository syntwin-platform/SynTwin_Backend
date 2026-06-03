namespace Syntwin.Application.Devices.Dtos;

public sealed class DevicePendingCommandResult
{
    public bool IsAuthenticated { get; set; }
    public bool IsDisabled { get; set; }
    public DeviceCommandPendingResponse? Command { get; set; }
}