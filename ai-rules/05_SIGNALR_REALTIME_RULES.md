# 05 - SignalR Realtime Rules

## Mục tiêu
SignalR dùng để backend đẩy telemetry realtime lên frontend. Frontend không cần biết telemetry đến từ simulate, HTTP device gateway hay MQTT.

## Hub chính
Tạo `TelemetryHub` trong `Syntwin.Api/Hubs`.

Route đề xuất:

```text
/hubs/telemetry
```

## Group rule
Mỗi robot có 1 group:

```text
robot-{robotId}
```

Client join group khi mở màn hình robot:

```csharp
JoinRobotGroup(string robotId)
LeaveRobotGroup(string robotId)
```

## Broadcast event
Event thống nhất:

```text
TelemetryUpdated
```

Payload nên dùng DTO rõ ràng gồm robotId, tọa độ, nhiệt độ, statusCode, timestamp.

## Phase MVP
Phase MVP dùng endpoint:

```text
POST /api/telemetry/simulate
```

Flow:

```text
TelemetryController -> TelemetryService -> Save TelemetryLog -> Update Robot.LastSeenAt -> Broadcast SignalR
```

## Phase Isaac Sim/MQTT sau này
Isaac Sim/MQTT không broadcast trực tiếp. Mọi nguồn telemetry phải đi qua `TelemetryService` rồi mới SignalR.

```text
MQTT/HTTP Adapter -> TelemetryService -> SignalR
```

## CORS rule cho SignalR
CORS phải:

- Cho phép origin frontend đang dùng.
- Cho phép credentials nếu SignalR dùng cookie/auth credential.
- Đặt `UseCors` trước `MapHub`.

## Không làm phức tạp MVP
- Không cần Redis backplane.
- Không cần scale-out SignalR.
- Không cần message broker cho telemetry simulate.
