# Phase 1 local regression

Tài liệu này xác nhận ba process cloud-ready vẫn giữ nguyên luồng local trước
khi bắt đầu Phase 2.

## 1. Data services

Từ thư mục `D:\EXE_SynTwin\SynTwin_Backend`:

```powershell
docker compose -f docker-compose.prod.yml up -d `
  syntwin-sqlserver `
  syntwin-redis `
  syntwin-influxdb

docker compose -f docker-compose.prod.yml ps
```

Port local mặc định:

```text
SQL Server: 11433
Redis:      16379
InfluxDB:   18086
```

## 2. Build và automated tests

```powershell
dotnet restore Syntwin.Backend.slnx
dotnet build Syntwin.Backend.slnx --no-restore
dotnet test Syntwin.Backend.slnx --no-build
```

Kết quả bắt buộc:

```text
Build: 0 errors, 0 warnings
Application tests: 19 passed
Integration tests: 7 passed
```

Integration tests cần Docker Desktop đang chạy.

## 3. Migrator idempotency

`Syntwin.DbMigrator` dùng user-secrets riêng. Cấu hình một lần:

```powershell
dotnet user-secrets set `
  --project src\Syntwin.DbMigrator `
  "ConnectionStrings:SyntwinDb" `
  "Server=localhost,11433;Database=SyntwinDb;User Id=sa;Password=<LOCAL_PASSWORD>;TrustServerCertificate=True"

dotnet user-secrets set `
  --project src\Syntwin.DbMigrator `
  "Seed:SuperAdmin:Enabled" `
  "false"
```

Chạy hai lần:

```powershell
dotnet run --project src\Syntwin.DbMigrator
$LASTEXITCODE

dotnet run --project src\Syntwin.DbMigrator
$LASTEXITCODE
```

Cả hai lần phải trả `0`. Lần hai phải báo database đã up to date.

## 4. Start Worker

Mở terminal riêng:

```powershell
dotnet run --project src\Syntwin.Worker
```

Kiểm tra:

```powershell
Invoke-WebRequest -UseBasicParsing `
  http://localhost:5201/health/live

Invoke-WebRequest -UseBasicParsing `
  http://localhost:5201/health/ready
```

Cả hai phải trả HTTP `200`.

## 5. Start API

Mở terminal riêng:

```powershell
dotnet run --project src\Syntwin.Api
```

Kiểm tra:

```powershell
Invoke-WebRequest -UseBasicParsing `
  http://localhost:5200/health/live

Invoke-WebRequest -UseBasicParsing `
  http://localhost:5200/health/ready

Invoke-WebRequest -UseBasicParsing `
  http://localhost:5200/swagger/index.html
```

Cả ba phải trả HTTP `200` trong Development.

## 6. Regression nghiệp vụ

Giữ Worker và API cùng chạy, sau đó kiểm tra:

1. Login và refresh token.
2. Company isolation.
3. Robot/device heartbeat.
4. Telemetry và SignalR.
5. Import Lua ngắn.
6. Import Lua 500-1500 steps.
7. Factory Run 6 robot.
8. Synchronized Factory Run.
9. Parallel-independent Factory Run.
10. Command timeout.
11. Robot chuyển Offline sau khi mất heartbeat.
12. LastSeen được flush từ Redis xuống SQL.
13. Reset command/program lỗi.

## 7. Resilience

Trong lúc API và Worker đang chạy:

1. Dừng Redis.
2. Xác nhận `/health/live` vẫn HTTP `200`.
3. Xác nhận `/health/ready` chuyển HTTP `503`.
4. Khởi động Redis lại.
5. Xác nhận readiness tự trở lại HTTP `200`.
6. Restart API và xác nhận Worker vẫn chạy.
7. Restart Worker và xác nhận API vẫn phục vụ request.

## 8. Gate trước Phase 2

- API không chứa `MigrateAsync`, `SeedSuperAdminAsync` hoặc
  `AddHostedService`.
- Worker chứa đúng bốn hosted service.
- Migrator chạy lặp với exit code `0`.
- API và Worker readiness đều healthy.
- Build và toàn bộ automated tests pass.
- Factory Run baseline không thay đổi.
- Không có secret thật trong Git.
