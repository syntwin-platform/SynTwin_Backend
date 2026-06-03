# 08 - Task Workflow Rules for Vibe Coding

## Quy tắc làm việc với AI/Cursor/Codex
Mỗi lần chỉ làm **1 task nhỏ**. Không yêu cầu AI code nhiều module cùng lúc.

## Trước khi sửa code
AI phải nói rõ:

1. Task đang làm là gì.
2. File nào sẽ tạo mới.
3. File nào sẽ chỉnh sửa.
4. Lệnh test/build sau khi xong.

Nếu AI không nói rõ file sẽ sửa, phải yêu cầu dừng lại.

## Trong khi code
- Không đổi architecture chính.
- Không tự thêm package nếu chưa cần.
- Không xóa code đang chạy nếu không giải thích.
- Không sửa nhiều lỗi ngoài phạm vi task.
- Nếu gặp lỗi, sửa lỗi nhỏ nhất trước.

## Sau mỗi task
AI phải cung cấp:

1. Tóm tắt thay đổi.
2. Danh sách file đã sửa.
3. Lệnh đã/ cần chạy.
4. Cách test bằng Swagger/Postman/CLI.
5. Lỗi thường gặp nếu có.
6. Dừng lại và chờ user báo `done`.

## Lệnh test mặc định
```bash
dotnet build
```

Nếu có Docker:

```bash
docker compose ps
docker compose logs syntwin-sqlserver --tail=50
```

Nếu có migration:

```bash
dotnet ef database update --project src/Syntwin.Infrastructure --startup-project src/Syntwin.Api
```

## Không generate quá dài
Nếu task lớn, bắt AI chia nhỏ:

```text
Task 1A: tạo DTO
Task 1B: tạo service interface
Task 1C: implement service
Task 1D: tạo controller
Task 1E: test Swagger
```

## Ưu tiên cao nhất
Luôn ưu tiên build pass và demo chạy được hơn code quá đẹp nhưng khó debug.
