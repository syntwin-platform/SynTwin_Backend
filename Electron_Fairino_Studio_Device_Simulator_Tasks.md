# Ke hoach trien khai Electron/Fairino-Studio lam Device Simulator cho Syntwin Backend

## Trang thai cap nhat 2026-06-09

Da hoan thanh trong working implementation:

- Config panel cho `backendUrl`, `robotId`, `deviceSecret` va cac interval.
- Device HTTP client cho heartbeat, telemetry, pending command va command result.
- Simulator lifecycle co chong request overlap.
- Telemetry mapping tu `robotStore` va `sceneStore`.
- Poll command va gui result `Completed`/`Failed`.
- Execute `MoveJ` co animation.
- Execute `MoveL` qua IK runner trong viewport 3D.
- Backend da co SignalR, offline monitor, telemetry throttle va command timeout.

Con lai de chot MVP:

- Execute `RunProgram` snapshot.
- Ho tro cac step `MoveTCP`, `RotateJoint`, `WaitMs`, `SetDO`, `GripperOpen`,
  `GripperClose`, `Comment`.
- Workflow sync len `RobotProgram`, publish va tao command `RunProgram`.
- Test end-to-end voi SQL Server, Redis va app Electron dang chay.
- Them automated tests cho device auth, duplicate result, timeout va ownership.

Ket qua kiem tra hien tai:

- Fairino-Studio `npm run build`: pass.
- Cac file Device Simulator moi: ESLint pass.
- Backend `dotnet build Syntwin.Backend.slnx`: pass, 0 warning, 0 error.

## 1. Muc tieu

Tai lieu nay mo ta chi tiet nhung viec can lam o phia Electron/Fairino-Studio de app co the dong vai tro `Device Simulator` thay Isaac Sim trong giai doan MVP.

Trong giai doan nay, Electron/Fairino-Studio se:

- Render robot 3D va workflow nhu hien tai.
- Gui heartbeat len Backend de bao robot online.
- Gui telemetry tu `robotStore` len Backend.
- Poll pending command tu Backend.
- Execute command tren local simulator/UI.
- Gui command result ve Backend.

Khi Isaac Sim that san sang, chi can tat simulator loop trong Electron va de Isaac Sim goi cung cac API device nay.

## 2. Flow tong quan

```text
Fairino-Studio Electron
  -> POST /api/device/heartbeat
  -> POST /api/device/telemetry
  -> GET  /api/device/commands/pending
  -> POST /api/device/commands/result

Syntwin Backend
  -> Verify RobotId + DeviceSecret
  -> Redis latest state / online TTL
  -> SQL command / result / audit
  -> SignalR realtime cho frontend viewer
```

## 3. Ket qua mong doi sau khi lam xong

- Electron nhap duoc `backendUrl`, `robotId`, `deviceSecret`.
- Bam `Connect` thi Backend nhan heartbeat va robot chuyen `Online`.
- Electron gui telemetry dinh ky tu state hien tai cua robot.
- Backend endpoint `GET /api/robots/{robotId}/state/latest` tra ve `tcpPose`, `jointAngles`, `statusCode`, `collisionWarning`.
- Khi Backend tao command `MoveJ`, Electron poll duoc command va cap nhat robot 3D.
- Khi Backend tao command `MoveL`, Electron poll duoc command va cap nhat TCP pose.
- Khi Backend tao command `RunProgram`, Electron chay cac steps snapshot theo dung thu tu.
- Sau moi command, Electron gui result `Completed` hoac `Failed` ve Backend.

## 4. Cau truc file de them vao Electron/Fairino-Studio

De nghi them cac file sau:

```text
src/renderer/src/services/backendDeviceClient.ts
src/renderer/src/services/backendDeviceSimulator.ts
src/renderer/src/services/backendCommandExecutor.ts
src/renderer/src/types/backendDevice.ts
src/renderer/src/components/BackendSimulatorPanel.tsx
```

Neu project hien co da co convention khac cho `services`, `types`, `components`, hay dat theo convention hien co.

## 5. Rang buoc Backend bat buoc FE phai biet

Section nay la phan quan trong de team Electron/Fairino-Studio khong phai doan contract Backend.

### 5.1. Backend local URL dung trong repo hien tai

Backend Syntwin hien tai dang khai bao trong:

```text
src/Syntwin.Api/Properties/launchSettings.json
```

URL local:

```text
http://localhost:5200
```

Default config phia Electron phai dung:

```ts
backendUrl: 'http://localhost:5200'
```

Khong mac dinh `http://localhost:5000` neu khong sua launch profile Backend.

### 5.2. CORS

Backend hien cho phep cac origin sau trong `appsettings.json`:

```text
http://localhost:3000
http://localhost:5173
http://localhost:4200
```

Neu renderer dev server cua Electron/Fairino-Studio chay origin khac, phai them origin do vao:

```text
src/Syntwin.Api/appsettings.json -> Cors:AllowedOrigins
```

Neu gap loi CORS, khong sua device API contract. Sua origin Backend truoc.

### 5.3. Hai loai auth khac nhau

Backend co 2 luong auth rieng:

```text
Device API auth:
  X-Robot-Id
  X-Device-Secret

User/Frontend API auth:
  Authorization: Bearer {accessToken}
```

Device API dung cho simulator/Isaac Sim:

```text
POST /api/device/heartbeat
POST /api/device/telemetry
GET  /api/device/commands/pending
POST /api/device/commands/result
```

User API dung cho UI user:

```text
POST /api/auth/login
POST /api/robots
GET  /api/robots/{robotId}/state/latest
POST /api/robots/{robotId}/programs
POST /api/robots/{robotId}/programs/{programId}/publish
POST /api/robots/{robotId}/commands
```

Khong dung JWT thay cho `X-Device-Secret` o Device API.
Khong dung `X-Device-Secret` thay cho JWT o User API.

### 5.4. Cach lay robotId va deviceSecret

RobotId va DeviceSecret lay tu response:

```text
POST /api/robots
Authorization: Bearer {accessToken}
```

Request:

```json
{
  "robotName": "FR5 Simulator",
  "model": "FR5",
  "connectionType": "HTTP",
  "ipAddress": null,
  "port": null
}
```

Response shape:

```json
{
  "robot": {
    "id": "11111111-1111-1111-1111-111111111111",
    "userId": "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa",
    "robotName": "FR5 Simulator",
    "model": "FR5",
    "connectionType": "HTTP",
    "status": "Registered",
    "lastSeenAt": null,
    "ipAddress": null,
    "port": null,
    "createdAt": "2026-06-08T00:00:00Z",
    "updatedAt": null
  },
  "deviceSecret": "raw-secret-only-returned-once"
}
```

Rang buoc:

- Backend chi tra raw `deviceSecret` khi create robot hoac reset secret.
- FE phai copy/luu secret lai cho simulator.
- Khong hien/log secret o console neu khong can debug local.

### 5.5. Device headers bat buoc

Moi request vao `/api/device/*` bat buoc co:

```http
X-Robot-Id: {robotId}
X-Device-Secret: {deviceSecret}
Content-Type: application/json
```

Neu thieu/sai:

```text
401 Unauthorized
```

Neu robot bi disabled:

```text
403 Forbidden
```

### 5.6. Telemetry payload dung theo code BE

Endpoint:

```text
POST /api/device/telemetry
```

Payload:

```json
{
  "robotId": "11111111-1111-1111-1111-111111111111",
  "tcpPose": {
    "x": 400,
    "y": 0,
    "z": 250,
    "rx": 0,
    "ry": 90,
    "rz": 0
  },
  "jointAngles": [0, -30, 90, 0, 60, 0],
  "temperature": null,
  "statusCode": "IDLE",
  "collisionWarning": false,
  "timestamp": "2026-06-08T00:00:00Z"
}
```

Rang buoc Backend:

- `robotId` trong body phai trung voi header `X-Robot-Id`.
- `jointAngles` bat buoc dung 6 phan tu.
- `statusCode` bat buoc khong rong.
- `tcpPose` bat buoc co du `x/y/z/rx/ry/rz`.
- Don vi: mm cho `x/y/z`, degree cho `rx/ry/rz` va joint angles.

Response:

```text
200 OK: telemetry accepted
400 Bad Request: payload sai
401 Unauthorized: sai secret
403 Forbidden: robot disabled
```

### 5.7. Pending command response dung theo code BE

Endpoint:

```text
GET /api/device/commands/pending
```

Neu khong co command:

```text
204 No Content
```

Neu co command:

```json
{
  "commandId": "22222222-2222-2222-2222-222222222222",
  "robotId": "11111111-1111-1111-1111-111111111111",
  "commandType": "MoveJ",
  "payload": {
    "jointAngles": [0, -30, 90, 0, 60, 0],
    "speed": 30,
    "acc": 30
  },
  "createdAt": "2026-06-08T00:00:00Z"
}
```

Rang buoc quan trong:

- Khi BE tra command, command da duoc mark `Sent`.
- FE phai gui result cho command do.
- Neu FE crash sau khi nhan command ma chua gui result, BE se timeout command sau thoi gian cau hinh.

### 5.8. Command result request dung theo code BE

Endpoint:

```text
POST /api/device/commands/result
```

Success:

```json
{
  "commandId": "22222222-2222-2222-2222-222222222222",
  "robotId": "11111111-1111-1111-1111-111111111111",
  "success": true,
  "status": "Completed",
  "message": "Simulated command executed in Fairino-Studio",
  "rawPayload": {
    "source": "Fairino-Studio Electron Simulator",
    "commandType": "MoveJ"
  },
  "completedAt": "2026-06-08T00:00:01Z"
}
```

Failed:

```json
{
  "commandId": "22222222-2222-2222-2222-222222222222",
  "robotId": "11111111-1111-1111-1111-111111111111",
  "success": false,
  "status": "Failed",
  "message": "JointAngles must contain exactly 6 values.",
  "rawPayload": {
    "source": "Fairino-Studio Electron Simulator",
    "commandType": "MoveJ"
  },
  "completedAt": "2026-06-08T00:00:01Z"
}
```

Rang buoc Backend:

- `status` chi chap nhan `Completed` hoac `Failed`.
- `success=true` phai di voi `status=Completed`.
- `success=false` phai di voi `status=Failed`.
- `robotId` trong body phai trung header.
- `message` toi da 500 ky tu.
- Backend idempotent theo `commandId`: gui lai result cung command se khong tao duplicate.

### 5.9. CommandType FE can support dung ten

Ten command/step can dung dung string enum Backend:

```text
EStop
ReturnHome
Start
Pause
ResetSimulation
MoveJ
MoveL
SetDO
RunProgram
```

Trong `RunProgram.steps`, step type dung:

```text
MoveJ
MoveL
RotateJoint
MoveTCP
SetDO
WaitMs
GripperOpen
GripperClose
Comment
```

Case-sensitive an toan nhat: gui dung PascalCase nhu tren.

### 5.10. Tao command tu UI user

Endpoint:

```text
POST /api/robots/{robotId}/commands
Authorization: Bearer {accessToken}
```

Request:

```json
{
  "commandType": "MoveJ",
  "payload": {
    "jointAngles": [0, -30, 90, 0, 60, 0],
    "speed": 30,
    "acc": 30
  }
}
```

Rang buoc Backend:

- User phai own robot.
- Super Admin khong duoc control robot customer.
- Plan phai cho phep send command, tuc Premium/plan co `CanSendCommand = true`.
- Basic/Free se bi block va ghi audit `COMMAND_BLOCKED_REQUIRE_PREMIUM`.
- Command duoc tao o status `Pending`, device simulator se poll o endpoint device.

### 5.11. RobotProgram API cho workflow sync

Create program:

```text
POST /api/robots/{robotId}/programs
Authorization: Bearer {accessToken}
```

Request:

```json
{
  "name": "fairino_workflow",
  "status": "Draft",
  "source": "Studio",
  "steps": [
    {
      "orderIndex": 1,
      "stepType": "MoveJ",
      "label": "MoveJ - Point 1",
      "payload": {
        "jointAngles": [0, -30, 90, 0, 60, 0],
        "speed": 30,
        "acc": 30
      }
    }
  ]
}
```

Rang buoc Backend:

- `name` bat buoc, toi da 100 ky tu.
- `steps` bat buoc co it nhat 1 step.
- `orderIndex >= 1`.
- `stepType` bat buoc dung enum.
- `label` bat buoc, toi da 150 ky tu.
- `payload` bat buoc.
- Program can publish truoc khi `RunProgram`.

Publish:

```text
POST /api/robots/{robotId}/programs/{programId}/publish
```

RunProgram command:

```json
{
  "commandType": "RunProgram",
  "payload": {
    "programId": "33333333-3333-3333-3333-333333333333"
  }
}
```

Backend se snapshot steps vao command payload. Electron device simulator khong can fetch program rieng khi execute `RunProgram`.

### 5.12. Latest state API de debug telemetry

Endpoint:

```text
GET /api/robots/{robotId}/state/latest
Authorization: Bearer {accessToken}
```

Dung endpoint nay de FE verify telemetry da vao Redis.

Response se co cac field chinh:

```json
{
  "robotId": "11111111-1111-1111-1111-111111111111",
  "isOnline": true,
  "status": "IDLE",
  "tcpPose": {
    "x": 400,
    "y": 0,
    "z": 250,
    "rx": 0,
    "ry": 90,
    "rz": 0
  },
  "jointAngles": [0, -30, 90, 0, 60, 0],
  "temperature": null,
  "collisionWarning": false,
  "lastSeenAt": "2026-06-08T00:00:00Z",
  "timestamp": "2026-06-08T00:00:00Z",
  "source": "Redis"
}
```

### 5.13. SignalR endpoint neu FE viewer can realtime

Hub:

```text
/hubs/telemetry
```

Auth:

```text
JWT Bearer hoac access_token query string khi dung SignalR client
```

Sau khi connect, FE phai invoke:

```text
JoinRobotGroup(robotId)
```

Backend se check ownership truoc khi add vao group.

Events:

```text
TelemetryUpdated
RobotStatusChanged
CommandCompleted
ProgramUpdated
```

Luu y: device simulator khong bat buoc dung SignalR. SignalR chu yeu cho viewer/dashboard.

### 5.14. Nhung dieu FE khong duoc gia dinh sai

- Khong gia dinh create robot la robot online. Robot chi online sau heartbeat/telemetry hop le.
- Khong gui telemetry neu chua co `robotId + deviceSecret`.
- Khong dung `payloadJson`; API response/request dung object `payload`.
- Khong tu mark command completed trong UI ma khong goi `/api/device/commands/result`.
- Khong execute `RunProgram` bang draft local neu command payload da co snapshot tu Backend.
- Khong poll command qua User API. Device simulator phai poll `/api/device/commands/pending`.
- Khong spam telemetry 60 FPS len Backend trong MVP. De nghi 250ms hoac theo config, SignalR Backend da throttle 100ms.

## 6. Task 1 - Them kieu du lieu dung chung

Tao file:

```text
src/renderer/src/types/backendDevice.ts
```

Noi dung can co:

```ts
export interface BackendSimulatorConfig {
  enabled: boolean
  backendUrl: string
  robotId: string
  deviceSecret: string
  heartbeatIntervalMs: number
  telemetryIntervalMs: number
  commandPollIntervalMs: number
}

export interface TcpPose {
  x: number
  y: number
  z: number
  rx: number
  ry: number
  rz: number
}

export interface DeviceTelemetryPayload {
  robotId: string
  tcpPose: TcpPose
  jointAngles: number[]
  temperature: number | null
  statusCode: string
  collisionWarning: boolean
  timestamp: string
}

export interface PendingDeviceCommand {
  commandId: string
  robotId: string
  commandType: string
  payload?: unknown
  createdAt: string
}

export interface DeviceCommandResultPayload {
  commandId: string
  robotId: string
  success: boolean
  status: 'Completed' | 'Failed'
  message: string
  rawPayload?: unknown
  completedAt: string
}

export interface BackendSimulatorStatus {
  isRunning: boolean
  isConnected: boolean
  lastHeartbeatAt?: string
  lastTelemetryAt?: string
  lastCommandAt?: string
  lastResultAt?: string
  lastError?: string
}
```

Default config de dung cho MVP:

```ts
export const defaultBackendSimulatorConfig: BackendSimulatorConfig = {
  enabled: false,
  backendUrl: 'http://localhost:5200',
  robotId: '',
  deviceSecret: '',
  heartbeatIntervalMs: 3000,
  telemetryIntervalMs: 250,
  commandPollIntervalMs: 1000
}
```

## 7. Task 2 - Them backend device API client

Tao file:

```text
src/renderer/src/services/backendDeviceClient.ts
```

Client nay chi phu trach goi HTTP. Khong doc store va khong execute command trong file nay.

Can co cac function:

```ts
postHeartbeat(config)
postTelemetry(config, telemetry)
getPendingCommand(config)
postCommandResult(config, result)
```

Header bat buoc:

```ts
{
  'Content-Type': 'application/json',
  'X-Robot-Id': config.robotId,
  'X-Device-Secret': config.deviceSecret
}
```

Endpoint can goi:

```text
POST {backendUrl}/api/device/heartbeat
POST {backendUrl}/api/device/telemetry
GET  {backendUrl}/api/device/commands/pending
POST {backendUrl}/api/device/commands/result
```

Luu y:

- Neu `GET /commands/pending` tra `204 No Content`, return `null`.
- Neu response khong OK, doc body neu co va throw error co message ro rang.
- Trim dau `/` cuoi cua `backendUrl` de tranh URL sai.
- Khong log `deviceSecret`.

Pseudo-code:

```ts
function deviceHeaders(config: BackendSimulatorConfig): HeadersInit {
  return {
    'Content-Type': 'application/json',
    'X-Robot-Id': config.robotId,
    'X-Device-Secret': config.deviceSecret
  }
}

function apiUrl(config: BackendSimulatorConfig, path: string): string {
  return `${config.backendUrl.replace(/\/+$/, '')}${path}`
}
```

## 8. Task 3 - Them config panel trong UI

Tao component:

```text
src/renderer/src/components/BackendSimulatorPanel.tsx
```

Panel can co cac truong:

- Backend URL.
- Robot ID.
- Device Secret.
- Heartbeat interval.
- Telemetry interval.
- Command poll interval.
- Nut `Connect`.
- Nut `Disconnect`.
- Trang thai simulator.
- Last heartbeat time.
- Last telemetry time.
- Last command time.
- Last error.

Luu config vao `localStorage`:

```text
syntwin.backendSimulator.config
```

Luu status runtime rieng trong React state, khong can persist.

Validation UI toi thieu:

- `backendUrl` khong rong.
- `robotId` khong rong va nen la GUID.
- `deviceSecret` khong rong.
- Interval khong duoi nguong an toan:
  - heartbeat >= 1000ms
  - telemetry >= 100ms
  - command poll >= 300ms

Vi tri panel:

- Co the dat trong sidebar/debug panel hien co.
- Neu chua co khu debug, dat mot panel nho trong settings/tools area.
- Khong can lam UI qua dep o phase dau, nhung phai ro trang thai connect/disconnect.

## 9. Task 4 - Build telemetry tu robotStore va sceneStore

Trong Fairino-Studio hien co cac state can map:

```text
robotStore:
  robotModel
  jointAngles
  tcpPose
  steps
  isPlaying
  currentStepIndex

sceneStore:
  collisionWarning
```

Tao function:

```ts
buildTelemetryFromStores(config: BackendSimulatorConfig): DeviceTelemetryPayload
```

Mapping:

```ts
const robotState = useRobotStore.getState()
const sceneState = useSceneStore.getState()

return {
  robotId: config.robotId,
  tcpPose: robotState.tcpPose,
  jointAngles: robotState.jointAngles,
  temperature: null,
  statusCode: robotState.isPlaying ? 'RUNNING' : 'IDLE',
  collisionWarning: sceneState.collisionWarning,
  timestamp: new Date().toISOString()
}
```

Validation truoc khi gui:

- `jointAngles` phai la array.
- `jointAngles.length === 6`.
- Moi joint angle phai la number finite.
- `tcpPose` phai co `x`, `y`, `z`, `rx`, `ry`, `rz`.
- Moi field trong `tcpPose` phai la number finite.

Quy uoc don vi:

- `tcpPose.x/y/z`: millimeter.
- `tcpPose.rx/ry/rz`: degree.
- `jointAngles`: degree, theo thu tu `j1..j6`.

## 10. Task 5 - Them backendDeviceSimulator loop

Tao file:

```text
src/renderer/src/services/backendDeviceSimulator.ts
```

File nay quan ly lifecycle:

- `start(config, callbacks)`
- `stop()`
- `isRunning()`

Khi start:

```text
1. Validate config.
2. Gui heartbeat ngay lap tuc.
3. Gui telemetry ngay lap tuc.
4. Poll command ngay lap tuc.
5. Tao interval heartbeat.
6. Tao interval telemetry.
7. Tao interval command poll.
```

Khi stop:

```text
1. clearInterval heartbeat timer.
2. clearInterval telemetry timer.
3. clearInterval command timer.
4. reset running flag.
```

Can tranh overlap request:

- Neu heartbeat truoc chua xong, bo qua tick heartbeat tiep theo.
- Neu telemetry truoc chua xong, bo qua tick telemetry tiep theo.
- Neu command poll truoc chua xong, bo qua tick poll tiep theo.

Trang thai can callback ve UI:

```ts
onStatusChange(status: Partial<BackendSimulatorStatus>): void
onLog?(message: string): void
```

Status update:

- Heartbeat OK -> `lastHeartbeatAt`, `isConnected: true`.
- Telemetry OK -> `lastTelemetryAt`.
- Poll co command -> `lastCommandAt`.
- Result sent -> `lastResultAt`.
- Error -> `lastError`, neu lien tuc loi network thi `isConnected: false`.

## 11. Task 6 - Them command executor

Tao file:

```text
src/renderer/src/services/backendCommandExecutor.ts
```

File nay nhan `PendingDeviceCommand`, apply vao store, va tra ve result success/failed.

Function chinh:

```ts
executeBackendCommand(command: PendingDeviceCommand): Promise<void>
```

Command can support trong MVP:

### MoveJ

Payload:

```json
{
  "jointAngles": [0, -30, 90, 0, 60, 0],
  "speed": 30,
  "acc": 30
}
```

Behavior:

- Validate `jointAngles.length === 6`.
- Apply vao `robotStore`.
- Neu store co function animation/playback thi co the animate.
- Neu chua co animation, set state truc tiep la chap nhan cho MVP.

### MoveL

Payload:

```json
{
  "tcpPose": {
    "x": 400,
    "y": 0,
    "z": 250,
    "rx": 0,
    "ry": 90,
    "rz": 0
  },
  "speed": 30,
  "acc": 30
}
```

Behavior:

- Validate `tcpPose`.
- Apply vao `robotStore`.
- Neu app co IK resolver thi co the resolve joint angles.
- Neu chua co IK, MVP co the chi update TCP pose/local marker.

### RunProgram

Payload tu Backend la snapshot:

```json
{
  "programId": "...",
  "programName": "...",
  "programStatus": "Published",
  "snapshottedAt": "2026-06-08T00:00:00Z",
  "steps": [
    {
      "orderIndex": 1,
      "stepType": "MoveJ",
      "label": "MoveJ - Point 1",
      "payload": {
        "jointAngles": [0, -30, 90, 0, 60, 0],
        "speed": 30,
        "acc": 30
      }
    }
  ]
}
```

Behavior:

- Validate `steps` la array.
- Sort theo `orderIndex`.
- Execute tung step.
- `Comment`: bo qua hoac log.
- `WaitMs`: delay theo `payload.delayMs`.
- `MoveJ`: apply joint angles.
- `MoveL` / `MoveTCP`: apply TCP pose.
- `RotateJoint`: update mot joint theo `jointIndex`.
- `SetDO`: mock/log, vi khong co hardware IO trong Electron.
- `GripperOpen` / `GripperClose`: mock/log hoac update gripper state neu app co.

Neu bat ky step nao loi, dung program va gui command result `Failed`.

## 12. Task 7 - Gui command result ve Backend

Sau khi execute command:

Success:

```ts
await postCommandResult(config, {
  commandId: command.commandId,
  robotId: command.robotId,
  success: true,
  status: 'Completed',
  message: 'Simulated command executed in Fairino-Studio',
  rawPayload: {
    source: 'Fairino-Studio Electron Simulator',
    commandType: command.commandType
  },
  completedAt: new Date().toISOString()
})
```

Failed:

```ts
await postCommandResult(config, {
  commandId: command.commandId,
  robotId: command.robotId,
  success: false,
  status: 'Failed',
  message: error instanceof Error ? error.message : 'Command execution failed',
  rawPayload: {
    source: 'Fairino-Studio Electron Simulator',
    commandType: command.commandType
  },
  completedAt: new Date().toISOString()
})
```

Luu y:

- Luon gui result sau khi nhan command, ke ca execute loi.
- Neu gui result loi do network, log UI va co the retry nhe.
- Backend da idempotent theo `commandId`, nen gui lai cung result khong tao duplicate.

## 13. Task 8 - Workflow sync len Backend de chay RunProgram

Phase nay co the lam sau khi MoveJ/MoveL command queue da chay on.

Can them API client phia user/JWT rieng voi device client:

```text
GET  /api/robots/{robotId}/programs
POST /api/robots/{robotId}/programs
GET  /api/robots/{robotId}/programs/{programId}
PUT  /api/robots/{robotId}/programs/{programId}
POST /api/robots/{robotId}/programs/{programId}/publish
POST /api/robots/{robotId}/commands
```

Mapping tu Fairino workflow:

```text
WorkflowStep.type  -> RobotProgramStep.stepType
WorkflowStep.label -> RobotProgramStep.label
index              -> RobotProgramStep.orderIndex
step fields        -> RobotProgramStep.payload
```

Payload create/update program:

```json
{
  "name": "fairino_workflow",
  "source": "Studio",
  "steps": [
    {
      "orderIndex": 1,
      "stepType": "MoveJ",
      "label": "MoveJ - Point 1",
      "payload": {
        "jointAngles": [0, -30, 90, 0, 60, 0],
        "speed": 30,
        "acc": 30
      }
    }
  ]
}
```

Run flow:

```text
1. Save workflow len RobotProgram.
2. Publish program.
3. User tao command RunProgram:
   POST /api/robots/{robotId}/commands
4. Backend tao Pending command co snapshot steps.
5. Electron device simulator poll duoc RunProgram.
6. Electron execute steps snapshot.
7. Electron gui result.
```

## 14. Thu tu trien khai khuyen nghi

### Phase A - Ket noi heartbeat

- Tao config type.
- Tao config panel.
- Tao API client.
- Start simulator chi gui heartbeat.
- Test Backend tra `200`.
- Test robot status chuyen Online.

Definition of Done:

- Nhap `robotId + deviceSecret`.
- Bam Connect.
- Backend heartbeat accepted.
- Robot online trong SQL/Redis.

### Phase B - Telemetry latest state

- Them `buildTelemetryFromStores`.
- Gui telemetry theo interval.
- Test `GET /api/robots/{robotId}/state/latest`.

Definition of Done:

- Latest state co `tcpPose`.
- Latest state co `jointAngles`.
- Latest state co `collisionWarning`.
- UI thay last telemetry time cap nhat.

### Phase C - Command MoveJ/MoveL

- Them command poll.
- Them command executor.
- Execute `MoveJ`.
- Execute `MoveL`.
- Gui command result.

Definition of Done:

- Backend command status di tu `Pending` -> `Sent` -> `Completed`.
- Robot trong Electron thay doi theo command.
- Neu payload loi, command thanh `Failed`.

### Phase D - RunProgram

- Execute snapshot steps.
- Support `MoveJ`, `MoveL`, `MoveTCP`, `RotateJoint`, `WaitMs`, `SetDO`, `GripperOpen`, `GripperClose`, `Comment`.
- Gui result sau khi program xong.

Definition of Done:

- Backend tao command `RunProgram`.
- Electron poll duoc snapshot.
- Electron chay steps theo order.
- Backend nhan result Completed/Failed.

### Phase E - Workflow sync

- Luu Fairino workflow len `RobotProgram`.
- Publish.
- Tao command `RunProgram`.

Definition of Done:

- Workflow trong Electron khong chi save local `.fairobot`, ma sync duoc len Backend.
- `RunProgram` chay snapshot da publish.

## 15. Checklist test chi tiet

### Device config

- Config rong thi khong cho Connect.
- Sai `robotId` format thi bao loi.
- Sai `deviceSecret` thi Backend tra `401`.
- Robot disabled thi Backend tra `403`.

### Heartbeat

- Dung secret: heartbeat `200`.
- Ngat simulator qua TTL: robot offline.
- Bat lai simulator: robot online lai.

### Telemetry

- `jointAngles` du 6 phan tu: accepted.
- `jointAngles` thieu/thua: local validation fail hoac Backend `400`.
- `robotId` trong body khac header: Backend `400`.
- Latest state doc duoc tu Backend.

### Command

- Khong co command: pending endpoint tra `204`, UI khong loi.
- `MoveJ` hop le: Electron update joint angles, result Completed.
- `MoveJ` loi: result Failed.
- `MoveL` hop le: Electron update TCP pose, result Completed.
- Gui lai result cung command: Backend tra duplicate/idempotent, khong tao result moi.

### RunProgram

- Program snapshot rong: Failed.
- Step khong support: Failed co message ro.
- `WaitMs` delay dung.
- Neu step thu 3 loi, program dung va gui Failed.
- Neu tat ca steps OK, gui Completed.

## 16. Nhung viec khong can lam ngay

Chua can lam trong phase dau:

- SignalR client trong Electron, neu Electron dang dong vai device simulator.
- InfluxDB telemetry history.
- Backend Lua generator.
- SceneObject/Asset cloud sync.
- Retry queue persistent khi mat mang lau.
- Login/JWT user flow trong cung panel simulator, tru khi can workflow sync ngay.

## 17. Ghi chu bao mat

- Khong log `deviceSecret` ra console.
- Khong dua `deviceSecret` vao telemetry/result raw payload.
- `DeviceSecret` luu localStorage chi phu hop MVP/dev local.
- Khi len production, can ma hoa hoac luu qua secure storage cua Electron.

## 18. Quyet dinh MVP

MVP nen uu tien:

```text
Config panel
  -> Heartbeat
  -> Telemetry
  -> Pending command poll
  -> MoveJ
  -> MoveL
  -> RunProgram snapshot execution
  -> Command result
```

Sau khi flow nay chay on dinh, moi tiep tuc lam workflow cloud sync, SignalR viewer, telemetry history va Lua generator backend.
