using Syntwin.Application.Devices.Dtos;

namespace Syntwin.Application.Devices.Interfaces;

public interface IDeviceGatewayService
{
    Task<bool?> HeartbeatAsync(
        Guid robotId,
        string deviceSecret,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<DevicePendingCommandResult> TakePendingCommandAsync(
     Guid robotId,
     string deviceSecret,
     bool isBusy = false,
     string? ipAddress = null,
     CancellationToken cancellationToken = default);

    Task<DeviceCommandResultSubmitResult> SubmitCommandResultAsync(
        Guid robotId,
        string deviceSecret,
        DeviceCommandResultRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<bool?> SubmitTelemetryAsync(
    Guid robotId,
    string deviceSecret,
    DeviceTelemetryRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
}
