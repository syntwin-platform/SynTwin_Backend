# 07 - Isaac Sim / MQTT Phase Later

## Nguyên tắc chính
Isaac Sim chưa tích hợp thật trong MVP. MVP chỉ chuẩn bị interface/contract để sau này thay Fake bằng thật mà không phá core backend.

## Không được làm
- Không để `CommandService` gọi thẳng Isaac Sim.
- Không để `TelemetryService` phụ thuộc MQTT trực tiếp.
- Không hard-code endpoint Isaac Sim trong business service.
- Không bắt MVP phải cài MQTT broker nếu chưa cần.

## Interface command sender
Application định nghĩa interface:

```csharp
public interface IRobotCommandSender
{
    Task<SendRobotCommandResult> SendAsync(
        Guid robotId,
        Guid commandId,
        RobotCommandType commandType,
        CancellationToken cancellationToken = default);
}
```

Phase A dùng:

```text
FakeRobotCommandSender
```

Phase C có thể thay bằng:

```text
MqttRobotCommandSender
HttpRobotCommandSender
```

## Device Gateway skeleton
Có thể chuẩn bị các endpoint sau, nhưng chưa cần implement thật:

```text
POST /api/device/heartbeat
POST /api/device/telemetry
GET  /api/device/commands/pending?robotId={robotId}
POST /api/device/commands/result
```

## MQTT topic gợi ý
```text
syntwin/robots/{robotId}/heartbeat
syntwin/robots/{robotId}/telemetry
syntwin/robots/{robotId}/commands
syntwin/robots/{robotId}/command-results
syntwin/robots/{robotId}/status
```

## Device authentication gợi ý
Header:

```text
X-Robot-Id: {robotId}
X-Device-Secret: {rawDeviceSecret}
```

Backend hash raw secret rồi so với `Robot.DeviceTokenHash`.

## Luồng phase sau
```text
Isaac Sim Adapter -> MQTT/HTTP -> Device Gateway -> TelemetryService -> SignalR -> Frontend
Frontend -> API Command -> CommandService -> IRobotCommandSender -> MQTT/HTTP -> Isaac Sim Adapter
```
