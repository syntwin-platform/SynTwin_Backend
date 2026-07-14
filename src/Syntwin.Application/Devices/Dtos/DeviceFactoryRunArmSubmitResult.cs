namespace Syntwin.Application.Devices.Dtos;

public sealed class DeviceFactoryRunArmSubmitResult
{
    public bool IsAuthenticated { get; set; }

    public bool IsDisabled { get; set; }

    public DeviceFactoryRunArmResponse? Response { get; set; }
}