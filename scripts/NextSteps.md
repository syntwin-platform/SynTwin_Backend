# SynTwin Backend Next Steps

Updated: 2026-06-23

## Immediate Checklist

1. Stop Visual Studio Debug.
2. Run full backend build:

```powershell
cd D:\EXE_SynTwin\SynTwin_Backend
dotnet build .\Syntwin.Backend.slnx
```

3. Confirm both local modes:

```powershell
curl.exe http://localhost:5200/health/ready
curl.exe http://localhost:18080/health/ready
```

4. Test Fairino Studio device connection:
   - Backend URL: `http://localhost:5200` for Debug, or `http://localhost:18080` for Docker.
   - Robot ID must match the created robot.
   - Device secret must be the latest secret returned by create/reset robot.
   - Expected state: `Running: Yes`, `Connected: Yes`.

5. Test full robot program flow:
   - Create company.
   - Create robot.
   - Connect Fairino Backend Simulator.
   - Import LUA preview.
   - Import LUA to program.
   - Publish program.
   - Run `RunProgram` command.
   - Confirm command becomes `Completed`.

## InfluxDB Plan

Goal: add telemetry history without overloading SQL Server.

### What InfluxDB Should Store

- High-frequency robot telemetry.
- TCP pose samples.
- Joint angle samples.
- Robot temperature.
- Collision warning state.
- Runtime session metrics.
- Command latency and command execution metrics.

### Suggested Measurements

```text
robot_telemetry
robot_runtime_session
robot_command_metrics
robot_state_sample
```

### Suggested Tags

```text
robotId
companyId
sessionId
model
source
```

### Suggested Fields

```text
joint1
joint2
joint3
joint4
joint5
joint6
tcp_x
tcp_y
tcp_z
tcp_rx
tcp_ry
tcp_rz
temperature
collision_warning
command_duration_ms
```

### Do Not Store In InfluxDB

- Passwords.
- JWT tokens.
- Device secrets.
- VNPAY secrets.
- User profile data.
- Payment business records.
- Company membership records.
- Robot program source of truth.

## InfluxDB Implementation Steps

1. Add local InfluxDB container to development compose.
2. Add config section:

```json
{
  "InfluxDb": {
    "Url": "http://localhost:8086",
    "Org": "syntwin",
    "Bucket": "robot-telemetry",
    "Token": ""
  }
}
```

3. Add application interface:

```text
IRobotTelemetryHistoryWriter
IRobotTelemetryHistoryReader
```

4. Implement InfluxDB writer in infrastructure.
5. In `DeviceGatewayService`, write telemetry to:
   - Redis for latest state.
   - InfluxDB for history.
6. Add history API endpoint:

```text
GET /api/robots/{robotId}/telemetry/history
```

7. Add retention policy for telemetry data.
8. Add failure-safe behavior: if InfluxDB is unavailable, robot realtime flow must continue.

## Google Cloud Deployment Plan

Recommended target architecture:

```text
Cloud Run:
- Syntwin.Api container

Cloud SQL for SQL Server:
- SyntwinDb

Memorystore for Redis:
- Runtime cache
- Command queue
- Distributed locks
- SignalR backplane

InfluxDB:
- Prefer InfluxDB Cloud Serverless
- Alternative: self-host InfluxDB on VM/GKE only if needed

Secret Manager:
- JWT signing key
- SQL password
- VNPAY hash secret
- VNPAY TMN code
- InfluxDB token
- Redis connection settings
```

## What To Deploy

Deploy:

- `Syntwin.Api` container image.
- Database migrations.
- Runtime configuration.
- Health checks.
- Swagger only for staging, not public production.
- Redis connection to managed Redis.
- SQL connection to managed SQL Server.
- InfluxDB telemetry writer.

Do not deploy as long-term production:

- Local SQL Server container.
- Local Redis container.
- Local Nginx container unless using VM/GKE.
- `.env` secrets.
- ngrok URLs.

## Cloud Readiness Checklist

- Replace ngrok VNPAY return URL with real domain.
- Move secrets out of `.env`.
- Disable Swagger on public production, or protect it.
- Confirm CORS allowed origins for production FE.
- Run database migrations against Cloud SQL.
- Configure Cloud SQL connection.
- Configure Redis private networking.
- Configure InfluxDB token and bucket.
- Confirm `/health/ready` works in cloud.
- Confirm VNPAY callback works with public HTTPS domain.
- Confirm Fairino/device gateway can reach cloud backend.

## Suggested Order

1. Finish local Fairino end-to-end robot program test.
2. Add InfluxDB locally.
3. Add telemetry history API.
4. Build and test Docker image cleanly.
5. Create Google Cloud staging resources.
6. Deploy API to Cloud Run.
7. Connect Cloud SQL and Redis.
8. Connect InfluxDB.
9. Test VNPAY with staging domain.
10. Test Fairino Studio against staging backend.
