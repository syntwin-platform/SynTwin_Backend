namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceCommandResultSubmitResult
{
    public bool IsAuthenticated { get; set; }

    public bool IsDisabled { get; set; }

    public DeviceCommandResultResponse? Result { get; set; }
}
