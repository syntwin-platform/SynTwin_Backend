# SynTwin Backend Progress

Updated: 2026-06-23

## Current Runtime Setup

- Debug backend runs on `http://localhost:5200`.
- Docker production stack `syntwin-prod` runs on `http://localhost:18080`.
- Docker services currently used:
  - `syntwin-api`
  - `syntwin-nginx`
  - `syntwin-sqlserver`
  - `syntwin-redis`
- SQL Server is exposed locally on `localhost:11433`.
- Redis is exposed locally on `localhost:16379`.
- Health checks are available:
  - `/health/live`
  - `/health/ready`
- `syntwin-prod` health check has passed through Nginx on `localhost:18080`.
- Debug health check has passed through Kestrel on `localhost:5200`.

## Scale Work Completed

- Added SQL Server and Redis health checks.
- Reduced noisy EF Core command logging in appsettings.
- Added Redis-based distributed lock support for background workers.
- Added Redis command queue hardening with command requeue behavior.
- Added `AsNoTracking()` to read-only repository queries.
- Fixed registration issue caused by no-tracking subscription plan by keeping `PlanId` only instead of attaching the plan entity.
- Added robot runtime metrics service.
- Added robot runtime session related infrastructure.
- Added command timeout scheduling support.
- Added last-seen flush background service.
- Added Docker production deployment files:
  - `.dockerignore`
  - `src/Syntwin.Api/Dockerfile`
  - `docker-compose.prod.yml`
  - `nginx/syntwin.conf`
- Added load-test script:
  - `scripts/load-test-robot-runtime.ps1`
- Enabled Swagger in `syntwin-prod` via `Swagger__Enabled: "true"` while keeping `ASPNETCORE_ENVIRONMENT=Production`.

## VNPAY Status

- VNPAY sandbox payment flow was tested successfully.
- Backend received VNPAY return callback through ngrok.
- Payment transaction was updated in SQL Server:
  - `Status = Paid`
  - `ResponseCode = 00`
  - `TransactionStatus = 00`
  - `PaidAt` and `ProcessedAt` were set.
- Browser redirect to `localhost:3000/payment/vnpay-return` failed only because FE was not running on port `3000`; backend payment processing itself succeeded.

## Current Port Decision

- Visual Studio Debug should keep using:
  - `http://localhost:5200`
- Docker `syntwin-prod` should keep using:
  - `http://localhost:18080`
- This avoids port conflicts and allows Debug and Docker to run side by side.
- For VNPAY testing, checkout, ngrok, return URL, and status check must all use the same mode:
  - Debug mode: `5200`
  - Docker mode: `18080`

## Fairino Studio / Robot Runtime Status

- Backend Program login from Fairino Studio works.
- Backend Simulator panel still needs final device connection verification.
- Expected successful simulator state:
  - `Running: Yes`
  - `Connected: Yes`
  - `Last heartbeat` has a timestamp
  - `Last telemetry` has a timestamp
  - commands move to `Completed`
- Redis logs showing background saves are normal and not an error.

## Known Verification Gap

- `dotnet build` could not complete because Visual Studio Debug was running and locking DLL files.
- Error source:
  - `Syntwin.Application.dll`
  - `Syntwin.Infrastructure.dll`
- This is not confirmed as a code compile error.
- Stop Visual Studio Debug, then run:

```powershell
dotnet build D:\EXE_SynTwin\SynTwin_Backend\Syntwin.Backend.slnx
```

## Current Confidence

- Docker production stack: working locally.
- Health checks: working.
- Swagger in Docker: working.
- VNPAY backend processing: working.
- Redis runtime foundation: working locally.
- Robot/Fairino end-to-end command execution: still needs final full pass.
- Cloud deployment: not started yet.
