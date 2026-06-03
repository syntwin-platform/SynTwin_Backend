# 03 - Database Rules

## Database stack
Dùng EF Core + SQL Server cho MVP.

## Entity chính
Các entity chính của MVP:

- User.
- Robot.
- TelemetryLog.
- AuditLog.
- RobotCommand.
- CommandResult.

## Rule migration
- Mỗi thay đổi schema phải tạo migration riêng, tên rõ nghĩa.
- Không sửa migration cũ nếu migration đó đã apply vào database chung.
- Nếu đang dev local và database chưa quan trọng, có thể reset migration nhưng phải nói rõ trước.
- Sau migration phải chạy `dotnet build` và `dotnet ef database update`.

## User
Field tối thiểu:

- Id.
- Email unique.
- PasswordHash.
- Role.
- SubscriptionPlan.
- Timezone.
- CreatedAt.

Không lưu raw password.

## Robot
Field tối thiểu:

- Id.
- UserId.
- RobotName.
- Model.
- IpAddress.
- Port.
- Status.
- DeviceTokenHash.
- LastSeenAt.
- ConnectionType.
- CreatedAt.
- UpdatedAt.

`DeviceTokenHash`, `LastSeenAt`, `ConnectionType` dùng để chuẩn bị Isaac Sim/MQTT phase sau.

## TelemetryLog
- Lưu robotId, coordinatesJson, temperature, statusCode, timestamp.
- MVP có thể lưu history ngắn, chưa cần tối ưu time-series phức tạp.
- Không đưa logic render 3D vào database.

## AuditLog append-only
AuditLog chỉ được insert và read.

Cấm tạo endpoint hoặc repository method:

```text
UpdateAuditLog
DeleteAuditLog
PUT /api/audit-logs/{id}
PATCH /api/audit-logs/{id}
DELETE /api/audit-logs/{id}
```

## Command tables
`RobotCommand` dùng để lưu request command. `CommandResult` dùng cho phase sau khi robot/Isaac Sim trả kết quả.

## Seed data
Chỉ seed dữ liệu demo nếu cần. Không seed password raw; nếu seed user thì password phải hash bằng BCrypt.
