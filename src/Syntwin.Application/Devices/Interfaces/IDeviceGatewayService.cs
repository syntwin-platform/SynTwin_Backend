using Syntwin.Application.Devices.Dtos;

namespace Syntwin.Application.Devices.Interfaces;

public interface IDeviceGatewayService
{
    Task<bool?> HeartbeatAsync(
        Guid robotId,
        string deviceSecret,
        CancellationToken cancellationToken = default);

    Task<DevicePendingCommandResult> TakePendingCommandAsync(
        Guid robotId,
        string deviceSecret,
        CancellationToken cancellationToken = default);

    Task<bool?> SubmitCommandResultAsync(
        Guid robotId,
        string deviceSecret,
        DeviceCommandResultRequest request,
        CancellationToken cancellationToken = default);

    Task<bool?> SubmitTelemetryAsync(
    Guid robotId,
    string deviceSecret,
    DeviceTelemetryRequest request,
    CancellationToken cancellationToken = default);
}
