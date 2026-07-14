# 02 - Coding Rules for .NET 9

## Mục tiêu coding style
Code phải dễ hiểu cho người mới, dễ debug, dễ build pass. Ưu tiên rõ ràng hơn ngắn gọn.

## Quy tắc C# cơ bản
- Dùng `nullable enable` theo project hiện tại.
- Tên class rõ nghĩa: `RobotService`, `CreateRobotRequest`, `TelemetryResponse`.
- Mỗi file chỉ nên chứa 1 class/record chính.
- Không viết method quá dài. Nếu method quá 80-100 dòng, tách nhỏ.
- Không dùng pattern phức tạp nếu chưa thật sự cần: CQRS, MediatR, event sourcing, generic repository quá trừu tượng.

## DTO rule
DTO phải rõ ràng, dễ đọc:

```csharp
public sealed class CreateRobotRequest
{
    public string RobotName { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string IpAddress { get; set; } = string.Empty;
    public int? Port { get; set; }
}
```

Người mới có thể dùng class thay vì record nếu dễ hiểu hơn.

## Service rule
Application service phải thể hiện rõ use case:

```text
Validate input
Get current user / robot
Check ownership / subscription
Update database
Write audit log if needed
Return response
```

## Error handling rule
- Không throw exception lung tung cho lỗi nghiệp vụ thường gặp.
- Có thể dùng `Result` đơn giản hoặc trả lỗi rõ từ service để Controller map sang HTTP status.
- Lỗi permission phải phân biệt: 401 chưa login, 403 không có quyền.

## Build rule
Sau mỗi task nhỏ phải chạy:

```bash
dotnet build
```

Nếu task liên quan EF migration:

```bash
dotnet ef migrations add <MigrationName> --project src/Syntwin.Infrastructure --startup-project src/Syntwin.Api
dotnet ef database update --project src/Syntwin.Infrastructure --startup-project src/Syntwin.Api
```

## Không generate quá dài
AI chỉ làm 1 task nhỏ mỗi lần. Không tạo toàn bộ backend trong 1 prompt.
