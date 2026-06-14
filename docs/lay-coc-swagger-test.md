# Chuyển `lay_coc.lua` thành Swagger command

Nguồn: `C:\Users\Dell\Downloads\lay_coc.lua`

Chuỗi được chuyển đổi nguyên trạng:

```text
MoveJ [0, -96.6, -83.5, 0, 0, 0]
MoveJ [-91.3, -88.5, -90, 0, 0, 0]
WaitMs 5000
MoveJ [-91.3, -88.5, -90, 0, 0, 0]
MoveJ [-175, -88.5, -90, 0, 0, 0]
```

Mọi lệnh `MoveJ` dùng `speed = 30` và `acc = 30`.

> Cảnh báo: bước cuối trong Lua mang nhãn `Move A`, nhưng J1 là `-175°`,
> không giống Move A đầu tiên có J1 là `0°`. File JSON giữ nguyên `-175°`.
> Phải xác nhận pose này trên teach pendant, vùng va chạm và giới hạn cáp trước khi chạy robot thật.

## Tạo program trên Swagger

1. Mở `http://localhost:5200/swagger`.
2. Authorize bằng `Bearer <accessToken>`.
3. Gọi `POST /api/robots/{robotId}/programs`.
4. Dùng toàn bộ nội dung file:
   `docs/swagger/lay-coc-program.json`.
5. Lưu `id` từ response thành `programId`.

## Publish

Gọi:

```text
POST /api/robots/{robotId}/programs/{programId}/publish
```

## Chạy chuỗi

Gọi:

```text
POST /api/robots/{robotId}/commands
```

Dùng `docs/swagger/lay-coc-run-command.json` và thay placeholder bằng `programId`:

```json
{
  "commandType": "RunProgram",
  "payload": {
    "programId": "<programId-from-create-response>"
  }
}
```

Fairino-Studio nhận command theo device polling và thực thi lần lượt 5 step.

## Chạy bằng PowerShell

Chỉ tạo và publish, chưa chuyển động:

```powershell
.\scripts\test-lay-coc-program.ps1 `
  -AccessToken "<access-token>" `
  -RobotId "<robot-guid>"
```

Sau khi xác nhận an toàn, gửi command và chờ hoàn tất:

```powershell
.\scripts\test-lay-coc-program.ps1 `
  -AccessToken "<access-token>" `
  -RobotId "<robot-guid>" `
  -Execute `
  -WaitForCompletion
```

Theo dõi thủ công bằng:

```text
GET /api/robots/{robotId}/commands
```

Trạng thái mong đợi:

```text
Pending -> Sent -> Completed
```

Swagger hiện gửi `RunProgram` tới device executor. Trong code hiện tại, Fairino-Studio
thực thi bằng simulator 3D. Muốn chạy trực tiếp cánh tay FAIRINO vật lý qua cùng command,
device executor phải được nối với FAIRINO SDK/controller.
