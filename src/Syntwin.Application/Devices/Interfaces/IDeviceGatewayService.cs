using Syntwin.Application.Devices.Dtos;

namespace Syntwin.Application.Devices.Interfaces;

public interface IDeviceGatewayService
{
    Task<DeviceSessionResponse?> CreateSessionAsync(
    Guid robotId,
    string deviceSecret,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);

    Task<bool?> HeartbeatWithSessionAsync(
    string accessToken,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);

    Task<bool?> HeartbeatAsync(
        Guid robotId,
        string deviceSecret,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<DevicePendingCommandResult> TakePendingCommandWithSessionAsync(
string accessToken,
bool isBusy = false,
int waitSeconds = 0,
string? ipAddress = null,
CancellationToken cancellationToken = default);

    Task<DevicePendingCommandResult> TakePendingCommandAsync(
  Guid robotId,
  string deviceSecret,
  bool isBusy = false,
  int waitSeconds = 0,
  string? ipAddress = null,
  CancellationToken cancellationToken = default);

    Task<DeviceCommandResultSubmitResult> SubmitCommandResultAsync(
        Guid robotId,
        string deviceSecret,
        DeviceCommandResultRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<DeviceFactoryRunArmSubmitResult> ArmFactoryRunCommandWithSessionAsync(
    string accessToken,
    DeviceFactoryRunArmRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);

    Task<DeviceFactoryRunStartedSubmitResult>
    ReportFactoryRunStartedWithSessionAsync(
        string accessToken,
        DeviceFactoryRunStartedRequest request,
        string? ipAddress = null,
        CancellationToken cancellationToken = default);

    Task<DeviceCommandResultSubmitResult> SubmitCommandResultWithSessionAsync(
    string accessToken,
    DeviceCommandResultRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);

    Task<bool?> SubmitTelemetryWithSessionAsync(
    string accessToken,
    DeviceTelemetryRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);

    Task<bool?> SubmitTelemetryAsync(
    Guid robotId,
    string deviceSecret,
    DeviceTelemetryRequest request,
    string? ipAddress = null,
    CancellationToken cancellationToken = default);
}
