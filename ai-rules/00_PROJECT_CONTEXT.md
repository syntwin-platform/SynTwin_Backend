# 00 - Project Context: Syntwin Backend

## Mục tiêu của dự án
Syntwin là nền tảng SaaS Digital Twin cho robot. Người dùng đăng ký tài khoản, quản lý robot của mình, theo dõi telemetry realtime, xem trạng thái robot và gửi lệnh điều khiển khi có quyền.

## Mục tiêu MVP trong 1 tuần
MVP backend cần chạy được demo ổn định, không ôm quá nhiều tính năng:

1. Auth cơ bản: Register, Login, JWT, BCrypt, `/me`.
2. Robot CRUD: user chỉ quản lý robot của chính mình.
3. Subscription mock: Free, Basic, Premium.
4. AuditLog append-only cho command và truy cập trái phép.
5. Telemetry giả lập + SignalR realtime.
6. Command permission + FakeRobotCommandSender.
7. Chuẩn bị skeleton cho Isaac Sim/MQTT nhưng chưa tích hợp thật.

## Vai trò backend
Backend là trung tâm quản lý dữ liệu, quyền hạn, realtime và gateway. Backend **không render 3D**. Phần 3D/WebGL/Isaac Sim thuộc frontend/simulation side.

Backend chỉ quản lý:

- User, profile, role, subscription.
- Robot ownership, robot status, device metadata.
- Telemetry logs và latest telemetry.
- Realtime broadcast qua SignalR.
- Command permission và command history.
- Audit logs bảo mật.
- Gateway contract để sau này Isaac Sim/robot endpoint gửi telemetry và nhận command.

## Trạng thái source hiện tại sau khi inspect zip
Source zip hiện tại có:

- Solution/project structure theo 4 project: `Syntwin.Api`, `Syntwin.Application`, `Syntwin.Domain`, `Syntwin.Infrastructure`.
- `docker-compose.yml` đã có SQL Server container.
- `appsettings.json` đã có connection string SQL Server local.
- Package JWT, Swagger, EF Core SQL Server, BCrypt đã xuất hiện trong csproj.
- Các project Application/Domain/Infrastructure vẫn gần như skeleton, còn `Class1.cs`.
- API vẫn có `WeatherForecastController` và `Program.cs` mặc định, chưa có business controller/service/entity chính.

=> Vì vậy AI phải đi từng bước nhỏ, ưu tiên build pass sau mỗi task.

## Những việc không làm trong MVP
- Không Kubernetes.
- Không microservices.
- Không Redis nếu chưa có nhu cầu rõ ràng.
- Không thanh toán Stripe/VNPay thật.
- Không email OTP/quên mật khẩu thật.
- Không AI predictive maintenance.
- Không troubleshoot firmware.
- Không tích hợp Isaac Sim thật ở giai đoạn đầu.
