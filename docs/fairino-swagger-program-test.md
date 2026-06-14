# Test chuỗi hành động FAIRINO bằng Swagger

Tài liệu này dùng API hiện có để:

1. Tạo một `RobotProgram` ở trạng thái `Draft`.
2. Publish program.
3. Gửi command `RunProgram`.
4. Theo dõi command cho tới khi `Completed` hoặc `Failed`.

> Cảnh báo: `RunProgram` có thể làm thiết bị đang kết nối chuyển động ngay lập tức.
> Luôn kiểm tra vùng làm việc, nút E-Stop, giới hạn khớp, TCP/tool và tải trước khi chạy.
> Các pose mẫu dưới đây được chọn gần pose khởi tạo của Fairino-Studio simulator,
> không phải pose đã được chứng nhận an toàn cho robot vật lý.

## Điều kiện trước khi test

- Backend chạy tại `http://localhost:5200`.
- Swagger mở tại `http://localhost:5200/swagger`.
- Tài khoản đang đăng nhập có quyền với Company chứa robot.
- Gói của Owner Company cho phép gửi command (`CanSendCommand = true`).
- Fairino-Studio đã kết nối bằng đúng `Robot ID` và `Device Secret`.
- Robot/device đang gửi heartbeat và polling pending command.

## 1. Authorize Swagger

Gọi `POST /api/auth/login`:

```json
{
  "email": "owner@example.com",
  "password": "your-password"
}
```

Lấy `accessToken` trong response. Nhấn **Authorize** trên Swagger và nhập:

```text
Bearer <accessToken>
```

## 2. Kiểm tra robot

Gọi `GET /api/robots`, sau đó lấy `id` của robot cần test.

Nên gọi thêm:

```text
GET /api/robots/{robotId}/state/latest
```

Chỉ tiếp tục khi đúng robot, đúng model và trạng thái thiết bị phù hợp.

## 3. Tạo program

Gọi:

```text
POST /api/robots/{robotId}/programs
```

Body:

```json
{
  "name": "FAIRINO Swagger smoke test",
  "status": "Draft",
  "source": "Studio",
  "steps": [
    {
      "orderIndex": 1,
      "stepType": "MoveJ",
      "label": "Move to test pose A",
      "payload": {
        "jointAngles": [0, -30, 90, 0, 60, 0],
        "speed": 10,
        "acc": 10
      }
    },
    {
      "orderIndex": 2,
      "stepType": "GripperOpen",
      "label": "Open gripper",
      "payload": {}
    },
    {
      "orderIndex": 3,
      "stepType": "WaitMs",
      "label": "Wait before moving",
      "payload": {
        "delayMs": 1000
      }
    },
    {
      "orderIndex": 4,
      "stepType": "MoveJ",
      "label": "Move to test pose B",
      "payload": {
        "jointAngles": [10, -35, 85, 0, 60, 10],
        "speed": 10,
        "acc": 10
      }
    },
    {
      "orderIndex": 5,
      "stepType": "GripperClose",
      "label": "Close gripper",
      "payload": {}
    },
    {
      "orderIndex": 6,
      "stepType": "WaitMs",
      "label": "Hold position",
      "payload": {
        "delayMs": 1000
      }
    },
    {
      "orderIndex": 7,
      "stepType": "SetDO",
      "label": "Turn cabinet DO2 on",
      "payload": {
        "doType": "cabinet",
        "doIndex": 2,
        "doValue": 1
      }
    },
    {
      "orderIndex": 8,
      "stepType": "MoveJ",
      "label": "Return to test pose A",
      "payload": {
        "jointAngles": [0, -30, 90, 0, 60, 0],
        "speed": 10,
        "acc": 10
      }
    },
    {
      "orderIndex": 9,
      "stepType": "GripperOpen",
      "label": "Release gripper",
      "payload": {}
    },
    {
      "orderIndex": 10,
      "stepType": "SetDO",
      "label": "Turn cabinet DO2 off",
      "payload": {
        "doType": "cabinet",
        "doIndex": 2,
        "doValue": 0
      }
    }
  ]
}
```

Lưu giá trị `id` trong response thành `programId`.

## 4. Publish program

Gọi:

```text
POST /api/robots/{robotId}/programs/{programId}/publish
```

Response hợp lệ phải có:

```json
{
  "status": "Published"
}
```

## 5. Chạy program

Sau khi kiểm tra lại pose và vùng làm việc, gọi:

```text
POST /api/robots/{robotId}/commands
```

Body:

```json
{
  "commandType": "RunProgram",
  "payload": {
    "programId": "<programId>"
  }
}
```

Response ban đầu phải có `status` là `Pending`. Fairino-Studio sẽ lấy command,
thực thi từng step và gửi kết quả về backend.

## 6. Theo dõi kết quả

Gọi lặp:

```text
GET /api/robots/{robotId}/commands
```

Tìm command vừa tạo theo `id`. Luồng trạng thái mong đợi:

```text
Pending -> Sent -> Completed
```

Nếu cần dừng khẩn cấp, gọi ngay:

```text
POST /api/robots/{robotId}/commands
```

```json
{
  "commandType": "EStop",
  "payload": {}
}
```

## Chạy tự động bằng PowerShell

Tạo và publish program nhưng chưa gửi lệnh chuyển động:

```powershell
.\scripts\test-fairino-program.ps1 `
  -AccessToken "<access-token>" `
  -RobotId "<robot-guid>"
```

Sau khi đã kiểm tra an toàn, gửi `RunProgram` và chờ kết quả:

```powershell
.\scripts\test-fairino-program.ps1 `
  -AccessToken "<access-token>" `
  -RobotId "<robot-guid>" `
  -Execute `
  -WaitForCompletion
```

## Lỗi thường gặp

- `401`: access token sai/hết hạn hoặc Device Secret sai.
- `403 Current subscription plan cannot send robot commands`: Owner Company chưa có gói cho phép command.
- `400 Only published robot programs can be run`: chưa gọi endpoint publish.
- Command đứng ở `Pending`: Fairino-Studio chưa kết nối hoặc chưa polling command.
- `Failed`: xem log Backend Simulator trong Fairino-Studio; thường do pose không hợp lệ,
  IK thất bại, DO index sai hoặc command bị E-Stop.

Hiện tại Fairino-Studio thực thi chuỗi này trên simulator 3D. Để điều khiển cánh tay
FAIRINO vật lý, lớp device executor cần được nối với FAIRINO SDK/controller thay cho
executor mô phỏng hiện tại.
