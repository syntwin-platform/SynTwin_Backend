# 01 - Architecture Rules

## Kiến trúc bắt buộc
Backend Syntwin đi theo **Modular Monolith + Clean Architecture đơn giản**.

Project chính:

```text
Syntwin.Api
Syntwin.Application
Syntwin.Domain
Syntwin.Infrastructure
```

## Dependency direction đúng
Luồng phụ thuộc chỉ được đi từ ngoài vào trong:

```text
Syntwin.Api -> Syntwin.Application -> Syntwin.Domain
Syntwin.Infrastructure -> Syntwin.Application
Syntwin.Infrastructure -> Syntwin.Domain
Syntwin.Api -> Syntwin.Infrastructure chỉ để đăng ký DI/config
```

## Nhiệm vụ từng layer

### Syntwin.Api
Chỉ chứa phần tiếp xúc HTTP/realtime:

- Controllers.
- SignalR Hubs.
- Middleware.
- Swagger config.
- Authentication/Authorization config.
- CORS config.
- Mapping endpoint.

Không chứa business logic dài.

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
