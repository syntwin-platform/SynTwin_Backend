using Syntwin.Application.Robots.Dtos;
using Syntwin.Application.Devices.Dtos;
namespace Syntwin.Application.Robots.Interfaces;

public sealed record RobotRuntimeStateCacheEntry(
    bool IsOnline,
    DateTimeOffset? LastSeenAt);

public interface IRobotStateCache
{
    Task SetOnlineAsync(
        Guid robotId,
        DateTimeOffset lastSeenAt,
        CancellationToken cancellationToken = default);

    Task<bool> IsOnlineAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<DateTimeOffset?> GetLastSeenAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<Guid, RobotRuntimeStateCacheEntry>> GetRuntimeStatesAsync(
    IReadOnlyCollection<Guid> robotIds,
    CancellationToken cancellationToken = default);

    Task SetLatestAsync(
        RobotLatestStateResponse state,
        CancellationToken cancellationToken = default);

    Task<bool> ShouldBroadcastTelemetryAsync(
    Guid robotId,
    TimeSpan minInterval,
    CancellationToken cancellationToken = default);

    Task<RobotLatestStateResponse?> GetLatestAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListPresenceDueRobotIdsAsync(
    DateTimeOffset dueAt,
    int take,
    CancellationToken cancellationToken = default);


    Task<IReadOnlyList<Guid>> ListLastSeenDirtyRobotIdsAsync(
    int take,
    CancellationToken cancellationToken = default);

    Task RemoveLastSeenDirtyAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task RemovePresenceDueAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task SetCurrentRuntimeSessionAsync(
    Guid robotId,
    Guid sessionId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default);

    Task<Guid?> GetCurrentRuntimeSessionIdAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task ClearCurrentRuntimeSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task SetDeviceSessionAsync(
    DeviceRuntimeSession session,
    TimeSpan ttl,
    CancellationToken cancellationToken = default);

    Task<DeviceRuntimeSession?> GetDeviceSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task ClearDeviceSessionAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

    Task AddTelemetryViewerAsync(
    Guid robotId,
    string connectionId,
    TimeSpan ttl,
    CancellationToken cancellationToken = default);

    Task RemoveTelemetryViewerAsync(
        Guid robotId,
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Guid>> ListTelemetryViewerRobotIdsAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    Task ClearTelemetryViewerConnectionAsync(
        string connectionId,
        CancellationToken cancellationToken = default);

    Task<bool> HasTelemetryViewersAsync(
        Guid robotId,
        CancellationToken cancellationToken = default);

}
