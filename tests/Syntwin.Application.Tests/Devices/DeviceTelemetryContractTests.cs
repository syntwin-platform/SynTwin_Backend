using System.Text.Json;
using Syntwin.Application.Devices.Dtos;

namespace Syntwin.Application.Tests.Devices;

public sealed class DeviceTelemetryContractTests
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web);

    [Fact]
    public void LegacyPayload_DeserializesWithoutExtendedTelemetry()
    {
        const string json =
            """
            {
              "robotId": "77497ee1-6ad6-44c6-8c59-6f6fa3093f54",
              "tcpPose": {
                "x": 100.1,
                "y": 200.2,
                "z": 300.3,
                "rx": 1.1,
                "ry": 2.2,
                "rz": 3.3
              },
              "jointAngles": [10, 20, 30, 40, 50, 60],
              "statusCode": "RUNNING",
              "collisionWarning": false,
              "timestamp": "2026-07-29T01:00:00Z"
            }
            """;

        var request = JsonSerializer.Deserialize<DeviceTelemetryRequest>(
            json,
            JsonOptions);

        Assert.NotNull(request);
        Assert.Null(request.SequenceNumber);
        Assert.Null(request.Io);
        Assert.Null(request.Execution);
        Assert.Null(request.Temperature);
        Assert.Equal("RUNNING", request.StatusCode);
        Assert.Equal(6, request.JointAngles.Count);
    }

    [Fact]
    public void ExtendedPayload_DeserializesIoExecutionAndSequence()
    {
        const string json =
            """
            {
              "robotId": "77497ee1-6ad6-44c6-8c59-6f6fa3093f54",
              "tcpPose": {
                "x": 100.1,
                "y": 200.2,
                "z": 300.3,
                "rx": 1.1,
                "ry": 2.2,
                "rz": 3.3
              },
              "jointAngles": [10, 20, 30, 40, 50, 60],
              "sequenceNumber": 42,
              "io": {
                "cabinetDigitalOutputs": {
                  "0": true,
                  "1": false
                },
                "toolDigitalOutputs": {
                  "0": true
                },
                "gripperState": "open"
              },
              "execution": {
                "currentCommandId": "f9bce10d-d1f2-4d2b-8526-526a513c1988",
                "state": "Running",
                "currentStepIndex": 2,
                "totalSteps": 5,
                "progressPercent": 60,
                "startedAt": "2026-07-29T01:00:00Z"
              },
              "statusCode": "RUNNING",
              "collisionWarning": false,
              "timestamp": "2026-07-29T01:00:01Z"
            }
            """;

        var request = JsonSerializer.Deserialize<DeviceTelemetryRequest>(
            json,
            JsonOptions);

        Assert.NotNull(request);
        Assert.Equal(42, request.SequenceNumber);
        Assert.NotNull(request.Io);
        Assert.True(request.Io.CabinetDigitalOutputs[0]);
        Assert.False(request.Io.CabinetDigitalOutputs[1]);
        Assert.True(request.Io.ToolDigitalOutputs[0]);
        Assert.Equal("open", request.Io.GripperState);
        Assert.NotNull(request.Execution);
        Assert.Equal("Running", request.Execution.State);
        Assert.Equal(2, request.Execution.CurrentStepIndex);
        Assert.Equal(5, request.Execution.TotalSteps);
        Assert.Equal(60, request.Execution.ProgressPercent);
    }
}
