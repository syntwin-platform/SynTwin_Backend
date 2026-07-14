# 04 - Auth and Security Rules

## JWT rule
- Dùng JWT Bearer cho API cần login.
- JWT phải có user id trong claim dễ lấy: `sub` hoặc `nameidentifier`.
- Không hard-code signing key production trong source.
- Swagger phải hỗ trợ Authorize bằng Bearer token.

## Password rule
- Mật khẩu phải hash bằng BCrypt.
- Không lưu raw password.
- Không log password.
- Không trả passwordHash ra API response.

## Refresh token rule nếu sau này làm
- Không lưu raw refresh token.
- Chỉ lưu refresh token hash.
- Token hết hạn phải bị reject.
- Logout/revoke phải đánh dấu token bị thu hồi.

## Ownership check bắt buộc
Mọi API lấy/sửa/xóa robot, telemetry, audit log theo robot phải check:

```text
robot.UserId == currentUserId
```

Nếu không khớp: trả 403 Forbidden.

## Premium check cho command
Remote command chỉ dành cho Premium user.

Flow bắt buộc:

```text
1. User đã login chưa?
2. Robot có tồn tại không?
3. Robot có thuộc user không?
4. User có Premium không?
5. Ghi AuditLog.
6. Gửi command qua IRobotCommandSender.
```

## SuperAdmin rule
SuperAdmin có thể xem log hệ thống và quản trị user trong phase sau. Nhưng SuperAdmin **không được điều khiển robot của user**.

## AuditLog bảo mật
Phải ghi log cho:

- Command hợp lệ.
- Command bị chặn vì không Premium.
- Truy cập robot không thuộc user nếu có currentUserId.
- Update IP/Port robot.

## Không tin frontend
Frontend có thể ẩn nút command cho user Basic, nhưng backend vẫn phải tự check. Không dựa vào frontend để bảo mật.
