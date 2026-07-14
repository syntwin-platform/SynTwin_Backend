namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunStartedSubmitResult
{
    public bool IsAuthenticated { get; set; }

    public bool IsDisabled { get; set; }

    public DeviceFactoryRunStartedResponse? Response { get; set; }
}