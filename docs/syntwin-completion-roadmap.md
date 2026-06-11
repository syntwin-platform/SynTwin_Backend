# SynTwin Completion Roadmap

Cap nhat: 2026-06-11

Tai lieu nay tong hop cac viec can lam tiep theo de hoan thien SynTwin Backend
va Web Frontend. Moi muc nen duoc thuc hien theo quy trinh:

```text
Scan code -> Add code -> Build -> Test -> Check diff -> Sang muc tiep theo
```

## 1. Quy uoc trang thai va uu tien

Trang thai:

- `DONE`: da code va da verify.
- `IN PROGRESS`: dang code, chua verify day du.
- `TODO`: chua lam.
- `WAITING`: dang cho module khac hoac contract.

Uu tien:

- `P0`: dang chan build, migration hoac flow chinh.
- `P1`: can cho MVP Owner/Monitor va robot operation.
- `P2`: can de hoan thien san pham.
- `P3`: hardening, toi uu va release.

## 2. Trang thai hien tai

### Backend

- Company va CompanyMember da co role `Owner`, `Monitor`.
- Robot da co `CompanyId`.
- Robot access da chuyen sang active Company membership.
- Monitor active da duoc mo Robot CRUD.
- Monitor active da duoc mo gui Command; subscription lay theo Owner Company.
- Robot va Command da ghi audit context co `companyId` va
  `actorCompanyRole`.
- Program dang duoc mo quyen cho Monitor va bo sung audit.
- SignalR join robot group da check Company membership.
- Device Gateway, heartbeat, telemetry, command polling/result da co.
- Migration `AddRobotCompanyOwnership` da co.

### Frontend

- Login password va email OTP da goi Backend.
- Company Owner/Monitor va Admin Company da goi Backend.
- Register, profile, pricing/payment, robot dashboard, factory dashboard,
  alerts, analytics va Admin Users van con mock hoac partial.
- Chua co shared API client va Company Context dung chung.
- Chua co SignalR client.

## 3. Immediate Blocker

### P0 - Hoan tat Robot Program compile

Trang thai: `IN PROGRESS`

Backend files:

```text
src/Syntwin.Application/RobotPrograms/Interfaces/IRobotProgramService.cs
src/Syntwin.Application/RobotPrograms/Services/RobotProgramService.cs
src/Syntwin.Api/Controllers/RobotProgramsController.cs
```

Can lam:

- Controller phai truyen `GetClientIpAddress()` vao:
  - `CreateAsync`
  - `UpdateAsync`
  - `PublishAsync`
  - `ArchiveAsync`
- Them helper `GetClientIpAddress()` trong controller.
- Format `RobotProgramService.cs`.
- Build lai Backend.
- Test Monitor create/update/publish/archive Program.
- Kiem tra audit:
  - `PROGRAM_CREATED`
  - `PROGRAM_UPDATED`
  - `PROGRAM_PUBLISHED`
  - `PROGRAM_ARCHIVED`

Done when:

```text
dotnet build Syntwin.Backend.slnx -c Release
0 Warning(s)
0 Error(s)
```

## 4. Company va Membership Module

Uu tien: `P1`

### Backend

Trang thai: `IN PROGRESS`

Can lam:

1. Them API disable/enable Monitor:

```http
PATCH /api/companies/{companyId}/monitors/{monitorUserId}/status
```

```json
{
  "isActive": false
}
```

2. Chi Owner duoc thay doi trang thai Monitor.
3. `GET /members` cua Owner phai tra ca Monitor active va inactive.
4. Tach y nghia:
   - Disable: giu membership, tam khoa quyen.
   - Enable: mo lai membership.
   - Remove: ket thuc lien ket theo quy tac nghiep vu da chot.
5. Ghi audit:
   - `MONITOR_ADDED`
   - `MONITOR_REPLACED`
   - `MONITOR_DISABLED`
   - `MONITOR_ENABLED`
   - `MONITOR_REMOVED`
6. Can nhac them:
   - `DisabledAt`
   - `DisabledByUserId`
   - `DisableReason`
7. Dam bao Owner khong the disable chinh minh.
8. Chot rule mot Company co mot Owner hay nhieu Owner; code hien tai nen coi
   `CreatedByUserId` la billing Owner.

### Frontend

Trang thai: `TODO`

Can lam:

- Company member table hien status Active/Disabled.
- Owner co nut Disable/Enable/Remove Monitor.
- Confirm dialog cho action nguy hiem.
- Monitor bi disable phai logout khoi Company context hoac chuyen sang Company
  khac con quyen.
- Khong suy ra Company role tu platform role.

Done when:

- Monitor bi disable mat REST access ngay lap tuc.
- Enable lai thi truy cap lai duoc.
- Owner thay duoc lich su thay doi membership.

## 5. Robot Module

Uu tien: `P1`

### Backend

Trang thai: `IN PROGRESS`

Can lam:

- Verify Owner va Monitor active deu:
  - create;
  - update;
  - disable;
  - reset device secret;
  - read latest state.
- Quota robot luon tinh theo subscription cua billing Owner.
- Chot quota la tong robot cua Owner tren moi Company hay theo tung Company.
  Hien tai code dang tinh tong tren cac Company Owner so huu.
- Can nhac them endpoint restore/enable Robot neu business can.
- Response can giu:
  - `companyId`
  - `currentUserRole`
  - `userId` cua nguoi tao de audit.
- Bo sung audit detail old/new values cho update.
- Khong ghi `deviceSecret` vao audit.

### Frontend

Trang thai: `TODO`

Can lam:

```text
lib/api/robots.ts
app/dashboard/robots/page.tsx
components/RobotFormDialog.tsx
components/DeviceSecretDialog.tsx
app/dashboard/robots/[robotId]/page.tsx
```

- Thay `mock-data` bang API.
- Moi request create gui `companyId`.
- Owner va Monitor active deu thay action CRUD.
- Hien secret mot lan sau create/reset.
- Khong luu secret vao localStorage.
- Hien loading, empty, error va quota state.
- Disable robot phai co confirm.

Done when:

- `/dashboard/robots` khong import `lib/mock-data.ts`.
- Owner va Monitor thay cung fleet cua Company.

## 6. Robot Command Module

Uu tien: `P1`

### Backend

Trang thai: `IN PROGRESS`

Can lam:

- Verify Monitor active gui duoc Command.
- Subscription `CanSendCommand` lay theo Company Owner.
- Owner Free/Basic va Monitor cung bi chan command.
- Audit phai co actor user, Company role, command type va payload.
- Them audit cho:
  - command delivered;
  - command completed;
  - command failed;
  - command timeout;
  - duplicate result;
  - blocked by disabled membership.
- Automated test race condition result vs timeout.
- Kiem tra EStop priority khi robot busy.

### Frontend

Trang thai: `TODO`

```text
lib/api/robot-commands.ts
components/RobotCommandPanel.tsx
components/CommandHistory.tsx
```

- Owner va Monitor active deu co operation controls.
- UI disable command neu billing Owner khong co `canSendCommand`.
- Ho tro EStop, ReturnHome, Start, Pause, ResetSimulation, MoveJ, MoveL,
  SetDO va RunProgram.
- Update history khi nhan SignalR `CommandCompleted`.
- EStop can thiet ke noi bat, khong nen dat sau nhieu click.

Done when:

- Monitor gui command, Device nhan va tra result.
- Owner xem duoc command do Monitor tao.
- Audit xac dinh dung actor.

## 7. Robot Program Module

Uu tien: `P1`

### Backend

Trang thai: `IN PROGRESS`

- Hoan tat P0 compile.
- Owner va Monitor active duoc create/update/publish/archive.
- Program audit va entity change phai save cung mot DbContext transaction.
- Archive idempotent nhung khong ghi audit trung lap.
- Chot co cho phep sua Program Published hay bat buoc tao version moi.
- Them `ProgramVersion` o giai doan sau neu can trace production.
- RunProgram chi chay Program Published va snapshot step tai thoi diem request.

### Frontend

Trang thai: `TODO`

```text
lib/api/robot-programs.ts
components/programs/ProgramEditor.tsx
components/programs/ProgramStepForm.tsx
app/dashboard/robots/[robotId]/programs/page.tsx
app/dashboard/robots/[robotId]/programs/[programId]/page.tsx
```

- CRUD Program va step editor.
- Reorder `orderIndex` bat dau tu 1.
- Ho tro day du step types.
- Publish/archive co confirm.
- Owner va Monitor active cung van hanh Program.
- Hien `createdByUserId` va last modified metadata neu Backend bo sung.

## 8. Audit Log Module

Uu tien: `P1`

### Backend

Trang thai: `TODO`

AuditLog hien chua co Company FK truc tiep. Can bo sung:

```csharp
Guid? CompanyId
Guid? TargetUserId
string? ActorCompanyRole
```

Can lam:

- Migration va indexes:
  - CompanyId + CreatedAt;
  - RobotId + CreatedAt;
  - UserId + CreatedAt;
  - Action + CreatedAt.
- Repository query co pagination/filter.
- Owner API:

```http
GET /api/companies/{companyId}/audit-logs
```

- Filter theo:
  - user;
  - robot;
  - action;
  - from/to;
  - page/pageSize.
- SuperAdmin API cho platform audit neu can.
- Khong ghi secret, password, token hash hay du lieu nhay cam.
- Chot retention theo subscription.

### Frontend

Trang thai: `TODO`

- Tao trang Company Activity/Audit.
- Filter actor, robot, action va time range.
- Hien role actor tai thoi diem thao tac.
- Link den Robot/Program/Command lien quan.
- Export CSV chi lam sau khi pagination/filter on dinh.

## 9. Realtime va SignalR Module

Uu tien: `P1`

### Backend

Trang thai: `IN PROGRESS`

- Owner va Monitor active join group duoc.
- Khi Owner disable Monitor dang online, phai ngung event ngay.
- Can quan ly connection theo `userId` hoac Company membership de revoke.
- Kiem tra membership lai tren action nhay cam.
- Chot event cho membership:
  - `CompanyMemberStatusChanged`
  - `AccessRevoked`
- Them integration test reconnect va revoke.

### Frontend

Trang thai: `TODO`

- Cai `@microsoft/signalr`.
- Tao:

```text
lib/realtime/robot-hub.ts
hooks/useRobotRealtime.ts
```

- Join/leave robot group dung vong doi component.
- Reconnect exponential backoff.
- Sau reconnect goi latest-state de dong bo.
- Khi nhan `AccessRevoked`, clear robot state va dieu huong.

## 10. Telemetry History Module

Uu tien: `P2`

### Backend

Trang thai: `TODO`

- Them InfluxDB vao Docker Compose.
- Tao `ITelemetryStore` va implementation InfluxDB.
- Ghi telemetry history trong Device Gateway.
- Them range query va downsampling API.
- Chot retention theo plan.
- Them health check SQL, Redis va InfluxDB.

API du kien:

```http
GET /api/robots/{robotId}/telemetry?from=&to=&bucket=
```

### Frontend

- Chart temperature, joint data, online/offline va collision history.
- Company/robot/time range filters.
- Dung timezone Company.
- Khong tai raw telemetry qua lon ve browser.

## 11. Alerts Module

Uu tien: `P2`

### Backend

Trang thai: `TODO`

- Tao Alert entity va severity/status enum.
- Tao alert tu:
  - collision;
  - over-temperature;
  - offline;
  - command failure/timeout;
  - device communication errors.
- API list/detail/acknowledge/resolve.
- Audit acknowledge/resolve.
- SignalR `AlertCreated` va `AlertUpdated`.

### Frontend

Trang thai: `WAITING`

- Thay hard-coded alerts o `/dashboard/alerts`.
- Filter Company/robot/severity/status/time.
- Owner va Monitor co the acknowledge theo policy can chot.
- Badge unread va realtime update.

## 12. Analytics Module

Uu tien: `P2`

### Backend

Trang thai: `TODO`

- KPI uptime.
- Command success/failure/timeout rate.
- Robot utilization.
- Temperature summary.
- Alert count.
- Program execution metrics.
- API aggregate theo Company, robot va time range.

### Frontend

Trang thai: `WAITING`

- Xoa `Math.random()` va mock analytics.
- Chart doc API aggregate.
- Comparison nhieu robot.
- Empty/loading/error/export states.

## 13. Auth, Profile, Subscription va Payment

Uu tien: `P2`

### Backend

- Review refresh token rotation va revoke.
- Review OTP rate limit va email production config.
- Chot billing Owner khi mot user co nhieu Company.
- VNPay idempotency, webhook/IPN verification va retry.
- Khong commit JWT, SQL, SMTP hay VNPay secret.

### Frontend

Trang thai: `PARTIAL`

- Tao shared API client co refresh mot lan.
- Chuyen Register khoi localStorage/mock.
- Profile doc/ghi Backend.
- Pricing doc `/api/subscription-plans`.
- VNPay checkout va return page.
- Dung ten plan Backend: `Free`, `Basic`, `Premium`.

## 14. SuperAdmin Module

Uu tien: `P2`

### Backend

- QA Admin Users va Admin Companies.
- Them pagination cho Admin Companies neu data tang.
- Admin khong duoc remote-control customer robot.
- Audit moi action admin.
- Admin dashboard metrics API.

### Frontend

- Thay `admin-mock-data`.
- Admin Users: search, pagination, lock/unlock, role va subscription.
- Admin Companies: QA Monitor management.
- Admin Dashboard: cho Backend metrics, khong aggregate toan bo data tai browser.

## 15. Shared Frontend Foundation

Uu tien: `P1`

Trang thai: `TODO`

Tao:

```text
lib/api/client.ts
lib/api/types.ts
lib/api/auth.ts
lib/company-context.tsx
components/CompanySwitcher.tsx
```

Can lam:

- Bearer token.
- Refresh token mot lan khi 401.
- Handle `204`.
- Parse `{ message, errors }`.
- Route guard Dashboard/Admin.
- Company selection persist hop ly.
- Capability khong chi dua vao platform session:
  - Company role;
  - active membership;
  - billing Owner plan.

## 16. Fairino Studio Integration

Uu tien: `P2`

- Xac nhan SetDO va Gripper end-to-end.
- Workflow sync Studio -> Backend Program.
- User JWT client tach khoi DeviceSecret client.
- Mapping local workflow sang Backend step contract.
- Publish va RunProgram.
- Automated tests payload parser, cancellation va workflow mapping.
- Khong de DeviceSecret thay the user authorization.

## 17. Automated Tests

Uu tien: `P2`

### Backend

- Tao test project.
- Authorization matrix:
  - Owner;
  - Monitor active;
  - Monitor disabled;
  - outsider;
  - SuperAdmin.
- Robot CRUD.
- Subscription quota.
- Command plan guard.
- Program audit.
- Membership disable/enable.
- SignalR revoke.
- Device authentication.
- Command timeout/result race.
- Migration upgrade tu database co robot cu.

### Frontend

- Unit test API client va mapping.
- Component test role/status/capability.
- E2E:
  - register/login;
  - Company + Monitor;
  - disable/enable Monitor;
  - robot CRUD;
  - command;
  - Program;
  - realtime;
  - payment;
  - SuperAdmin.

## 18. Security va Release Hardening

Uu tien: `P3`

- Di chuyen secret sang environment/user-secrets.
- Review CORS, HTTPS, JWT signing key va production logs.
- Rate limit auth va command endpoint nhay cam.
- Validate payload size.
- Audit retention/cleanup job.
- Database backup/restore rehearsal.
- Health endpoint cho SQL, Redis, InfluxDB.
- Structured logging va correlation ID.
- Accessibility, responsive va error boundary FE.
- Xoa business mock data va dead code.
- Viet README, environment setup va API test collection.

## 19. Thu tu trien khai khuyen nghi

```text
1. Fix Program compile va test Monitor Program
2. Disable/enable Monitor + membership audit
3. AuditLog schema + Company Audit API
4. SignalR revoke khi Monitor bi disable
5. Shared FE API client + Company Context
6. FE Robot CRUD
7. FE Command + Program
8. FE realtime Factory
9. Auth/Profile/Payment FE
10. Automated tests
11. Telemetry history/InfluxDB
12. Alerts
13. Analytics
14. SuperAdmin UI/metrics
15. Security va release hardening
```

## 20. Lenh verify chuan

Backend:

```powershell
cd D:\EXE_SynTwin\SynTwin_Backend
dotnet format Syntwin.Backend.slnx --verify-no-changes
dotnet build Syntwin.Backend.slnx -c Release
git diff --check
git status --short
```

Frontend:

```powershell
cd D:\EXE_SynTwin\SynTwin
npm.cmd exec tsc -- --noEmit
npm.cmd run lint
npm.cmd run build
git diff --check
git status --short
```

Fairino Studio:

```powershell
cd D:\EXE_SynTwin\Fairino-Studio
npm.cmd run build
```

## 21. Dieu kien hoan thanh MVP

- Owner quan ly Company va Monitor.
- Owner disable/enable Monitor tuc thoi.
- Owner va Monitor active van hanh Robot, Command va Program.
- Moi action nhay cam co audit va truy van duoc theo Company.
- Subscription/quota lay theo billing Owner.
- Device Gateway hoat dong end-to-end.
- FE khong con mock trong flow auth, company, robot, command, program va
  realtime.
- SignalR revoke quyen dung.
- Automated authorization tests pass.
- Khong commit secret.
- Backend va Frontend production build pass.
