# 01 - Architecture Rules

## Kiến trúc bắt buộc
Backend Syntwin đi theo **Modular Monolith + Clean Architecture đơn giản**.

Project chính:

```text
Syntwin.Api
Syntwin.Worker
Syntwin.DbMigrator
Syntwin.Hosting
Syntwin.Application
Syntwin.Domain
Syntwin.Infrastructure
```

## Dependency direction đúng
Luồng phụ thuộc chỉ được đi từ ngoài vào trong:

```text
Syntwin.Api -> Syntwin.Hosting
Syntwin.Api -> Syntwin.Application
Syntwin.Api -> Syntwin.Infrastructure chỉ để đăng ký DI/config
Syntwin.Worker -> Syntwin.Hosting
Syntwin.Worker -> Syntwin.Infrastructure
Syntwin.DbMigrator -> Syntwin.Infrastructure
Syntwin.Hosting -> Syntwin.Infrastructure
Syntwin.Hosting -> Syntwin.Application
Syntwin.Application -> Syntwin.Domain
Syntwin.Infrastructure -> Syntwin.Application
Syntwin.Infrastructure -> Syntwin.Domain
```

## Nhiệm vụ từng layer

### Syntwin.Api
Chứa HTTP pipeline và public endpoints:

- Controllers.
- Middleware.
- Swagger config.
- Authentication/Authorization config.
- CORS config.
- Mapping controller, SignalR và health endpoints.

Không chứa business logic dài.
Không chạy migration, seed hoặc background monitor.

### Syntwin.Worker
Chứa các tác vụ nền chạy liên tục:

- Robot offline monitor.
- Robot command timeout monitor.
- Factory Run lock maintenance.
- Robot LastSeen flush.

Worker dùng Redis distributed lock để nhiều replica không xử lý trùng.
Worker không expose controller hoặc public SignalR hub.

### Syntwin.DbMigrator
Là one-shot process:

- Apply EF Core migrations.
- Seed dữ liệu hệ thống theo cách idempotent.
- Thành công trả exit code `0`, lỗi trả exit code `1`.

API và Worker không được tự chạy migration.

### Syntwin.Hosting
Chứa hosting adapters dùng chung giữa API và Worker:

- SignalR Hub type và notifier implementation.
- Redis SignalR backplane registration.
- Dependency health checks.
- Startup configuration validation.

`Syntwin.Api` vẫn là process duy nhất map public SignalR endpoint.

### Syntwin.Application
Chứa use case/service:

- AuthService.
- RobotService.
- TelemetryService.
- CommandService.
- AuditLogService.
- DTOs.
- Interfaces: `IPasswordHasher`, `IJwtTokenGenerator`, `IRobotCommandSender`, repository/unit-of-work nếu dùng.

### Syntwin.Domain
Chứa core model:

- Entities.
- Enums.
- Domain constants.
- Rule đơn giản gắn với entity nếu cần.

Domain **không phụ thuộc EF Core**, **không phụ thuộc ASP.NET**, **không đọc configuration**.

### Syntwin.Infrastructure
Chứa triển khai kỹ thuật:

- EF Core DbContext.
- Entity configurations.
- Repository implementations nếu dùng.
- JWT generator.
- BCrypt password hasher.
- FakeRobotCommandSender.
- MQTT/HTTP sender ở phase sau.
- DependencyInjection extension.

## Những điều cấm
- Cấm để Domain reference EF Core hoặc ASP.NET Core.
- Cấm viết SQL/DbContext trực tiếp trong Controller nếu đã có service.
- Cấm để `CommandService` gọi thẳng Isaac Sim.
- Cấm để `TelemetryService` phụ thuộc MQTT/Isaac Sim trực tiếp.
- Cấm tạo microservices mới trong MVP.
- Cấm đổi architecture chính nếu không có lý do rõ ràng.
- Cấm đưa migration hoặc bốn background monitor trở lại API startup.

Ba executable host vẫn dùng chung Domain, Application, Infrastructure,
SQL Server và Redis. Đây là modular monolith nhiều process, không phải
microservices tách domain.

## Rule dễ hiểu cho AI khi code
Mỗi module nên đi theo flow:

```text
Controller -> Application Service -> Infrastructure/DbContext -> Database
```

Ví dụ:

```text
RobotsController -> RobotService -> SyntwinDbContext -> SQL Server
```

Nếu code bắt đầu quá phức tạp, phải đơn giản hóa trước khi tiếp tục.
