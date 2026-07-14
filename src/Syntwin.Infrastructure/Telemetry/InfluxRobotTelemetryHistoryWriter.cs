using InfluxDB.Client;
using InfluxDB.Client.Api.Domain;
using InfluxDB.Client.Writes;
using Microsoft.Extensions.Options;
using Syntwin.Application.Telemetry.Dtos;
using Syntwin.Application.Telemetry.Interfaces;

namespace Syntwin.Infrastructure.Telemetry;

public sealed class InfluxRobotTelemetryHistoryWriter : IRobotTelemetryHistoryWriter, IDisposable
{
    private const string MeasurementName = "robot_telemetry";

    private readonly InfluxDbOptions _options;
    private readonly InfluxDBClient? _client;

    public InfluxRobotTelemetryHistoryWriter(IOptions<InfluxDbOptions> options)
    {
        _options = options.Value;

        if (!_options.Enabled ||
            string.IsNullOrWhiteSpace(_options.Url) ||
            string.IsNullOrWhiteSpace(_options.Token) ||
            string.IsNullOrWhiteSpace(_options.Org) ||
            string.IsNullOrWhiteSpace(_options.Bucket))
        {
            return;
        }

        _client = new InfluxDBClient(
            _options.Url,
            _options.Token);
    }

    public async Task WriteAsync(
        RobotTelemetryHistoryWriteRequest request,
        CancellationToken cancellationToken = default)
    {
        if (_client is null)
        {
            return;
        }

        if (request.RobotId == Guid.Empty)
        {
            return;
        }

        var point = CreatePoint(request);
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);

        timeoutCts.CancelAfter(TimeSpan.FromSeconds(
            Math.Max(1, _options.WriteTimeoutSeconds)));

        await _client.GetWriteApiAsync().WritePointAsync(
            point,
            _options.Bucket,
            _options.Org,
            timeoutCts.Token);
    }

    public void Dispose()
    {
        _client?.Dispose();
    }

    private static PointData CreatePoint(RobotTelemetryHistoryWriteRequest request)
    {
        var timestamp = request.Timestamp == default
            ? request.ReceivedAt
            : request.Timestamp;

        var point = PointData
            .Measurement(MeasurementName)
            .Tag("robotId", request.RobotId.ToString());

        if (request.CompanyId.HasValue)
        {
            point = point.Tag("companyId", request.CompanyId.Value.ToString());
        }

        if (request.RuntimeSessionId.HasValue)
        {
            point = point.Tag("sessionId", request.RuntimeSessionId.Value.ToString());
        }

        if (!string.IsNullOrWhiteSpace(request.Model))
        {
            point = point.Tag("model", request.Model.Trim());
        }

        if (!string.IsNullOrWhiteSpace(request.Source))
        {
            point = point.Tag("source", request.Source.Trim());
        }

        point = point.Field("status_code", request.Status);

        if (request.JointAngles.Count >= 6)
        {
            point = point
                .Field("joint1", request.JointAngles[0])
                .Field("joint2", request.JointAngles[1])
                .Field("joint3", request.JointAngles[2])
                .Field("joint4", request.JointAngles[3])
                .Field("joint5", request.JointAngles[4])
                .Field("joint6", request.JointAngles[5]);
        }

        if (request.TcpPose is not null)
        {
            point = point
                .Field("tcp_x", request.TcpPose.X)
                .Field("tcp_y", request.TcpPose.Y)
                .Field("tcp_z", request.TcpPose.Z)
                .Field("tcp_rx", request.TcpPose.Rx)
                .Field("tcp_ry", request.TcpPose.Ry)
                .Field("tcp_rz", request.TcpPose.Rz);
        }

        if (request.Temperature.HasValue)
        {
            point = point.Field("temperature", request.Temperature.Value);
        }

        if (request.CollisionWarning.HasValue)
        {
            point = point.Field("collision_warning", request.CollisionWarning.Value);
        }

        return point.Timestamp(
            timestamp.UtcDateTime,
            WritePrecision.Ns);
    }
}