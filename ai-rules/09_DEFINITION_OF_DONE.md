# 09 - Definition of Done

## Checklist chung cho mọi task
Một task chỉ được xem là DONE khi:

- Đúng phạm vi task.
- Không phá architecture.
- Không tự thêm tính năng ngoài yêu cầu.
- `dotnet build` pass.
- Có cách test rõ ràng.
- Có tóm tắt file đã sửa.
- Có ghi chú lỗi thường gặp nếu task dễ lỗi.

## Checklist cho task database
- Entity/config đúng tên.
- Migration tạo được.
- Database update được.
- Không mất dữ liệu quan trọng nếu không được phép.
- Không tạo update/delete cho AuditLog.

## Checklist cho task API
- Swagger mở được.
- Endpoint đúng route.
- Request/response DTO rõ ràng.
- Status code hợp lý: 200/201/202/400/401/403/404.
- Endpoint cần login có `[Authorize]`.

## Checklist cho task Auth
- Password hash bằng BCrypt.
- Login trả JWT.
- Sai email/password không leak thông tin nhạy cảm.
- Swagger Authorize dùng được.
- `/api/auth/me` lấy đúng user hiện tại.

## Checklist cho Robot CRUD
- User chỉ thấy robot của mình.
- User không sửa/xóa robot của user khác.
- FREE giới hạn tối đa 1 robot nếu task đã yêu cầu.

## Checklist cho Telemetry/SignalR
- POST simulate lưu TelemetryLog.
- Robot `LastSeenAt` được cập nhật.
- Robot status chuyển Online khi có telemetry.
- SignalR broadcast đúng group `robot-{robotId}`.

## Checklist cho Command
- Chưa login: 401.
- Không sở hữu robot: 403 + audit nếu có current user.
- Basic/Free: 403 Require Premium + audit.
- Premium hợp lệ: command accepted + audit.
- CommandService chỉ gọi `IRobotCommandSender`, không gọi thẳng Isaac Sim.

## Checklist cho Docker
- SQL Server container chạy.
- API local connect được DB container.
- Migration chạy được.
- API container chạy được.
- Swagger mở được trong Docker.
- Không hard-code secret production.

## Câu chốt trước khi báo DONE
Task này build pass chưa? Test cụ thể bằng lệnh nào? Nếu chưa chạy test được thì phải nói rõ lý do.
