# Architecture — Smart Torque Guidance & Traceability System

| Item | Value |
|---|---|
| Document | Technical architecture |
| Version | 1.0 |
| Read with | `PRD.md` (what), `Rules.md` (constraints), `Phases.md` (order), `Design.md` (UI), `Memory.md` (state) |
| Stack | C# · .NET 8 (or 9) · ASP.NET Core MVC · Razor Views · EF Core · MS SQL Server · SignalR |
| Style | Layered MVC with a service layer and a data layer. One app, one DB, one codebase for both instruments. |

---

## 1. System Context

```
 ┌─────────────────────┐    USB     ┌──────────────────────┐   Wi-Fi (HTTP / WebSocket)   ┌───────────────────────────┐
 │ Digital Torque      │──────────▶│                      │─────────────────────────────▶│  ASP.NET Core MVC App     │
 │ Wrench              │           │   ESP32 Gateway      │                              │  (Razor Views + API)      │
 ├─────────────────────┤           │   (Transmitter ID)   │◀─────────────────────────────│                           │
 │ Digital Vernier     │──────────▶│                      │   target values / commands   │  Controllers · Services   │
 │ Caliper             │    USB    └──────────────────────┘                              │  Repositories · EF Core   │
 └─────────────────────┘                                                                  └─────────────┬─────────────┘
                                                                                                        │
                                                         Browser (Admin / Worker) ◀── Razor + SignalR ──┤
                                                                                                        ▼
                                                                                              ┌──────────────────┐
                                                                                              │   MS SQL Server  │
                                                                                              └──────────────────┘
```

Actors: **Admin** (browser), **Worker** (browser, usually tablet), **Device** (ESP32 posting readings), **SQL Server**.

---

## 2. Core Design Decision: One App, Both Instruments, Selected by Design

The instrument is chosen **per template step**, not per app or per worker. The server, device and UI all follow the step.

| Level | What is chosen | Effect |
|---|---|---|
| Template step (primary) | `Torque` or `Vernier` per step | One panel can mix 8 torque bolts and 3 Vernier checks |
| Template mode (convenience) | `Torque` / `Vernier` / `Mixed` | Pre-fills step instruments in the wizard only |
| Device | Each ESP32 channel registered with an instrument type | Readings routed to the right step |
| Worker screen | Switches automatically per step | No manual toggle |

| Aspect | Torque step | Vernier step |
|---|---|---|
| Unit | N·m | mm |
| Target / tolerance | Target torque ± tolerance | Target dimension ± tolerance |
| Live display | Gauge, peak torque capture | Digital readout, value on hold/button |
| Accept rule | Stable / peak reading | Stable reading or caliper data button |
| Result | PASS/FAIL vs tolerance | PASS/FAIL vs tolerance |

```csharp
public enum InstrumentType { Torque = 1, Vernier = 2 }

public interface IMeasurementEvaluator
{
    EvaluationResult Evaluate(decimal target, decimal tolerancePlus, decimal toleranceMinus,
                              ToleranceMode mode, decimal actual);
}

public interface IInstrumentHandler
{
    InstrumentType Type { get; }
    string Unit { get; }                       // "N·m" or "mm"
    bool ShouldAccept(DeviceReading reading);  // stable / peak / button rule
    LiveViewModel ToLiveView(DeviceReading reading, TemplateStep step);
}

// Resolved per step, so one session can use both
public interface IInstrumentHandlerFactory
{
    IInstrumentHandler For(InstrumentType type);   // TorqueHandler or VernierHandler
}
```

A new instrument type is added by implementing one more `IInstrumentHandler` and registering it. Controllers and views do not change.

---

## 3. Solution Structure

```
SmartTorque.sln
├── SmartTorque.Web              ASP.NET Core MVC, Razor, wwwroot, SignalR hubs
│   ├── Controllers/
│   │   ├── AccountController, AdminController
│   │   ├── TemplateController, ProductController
│   │   ├── BmController, WorkloadController
│   │   ├── ReportController, DeviceController, UserController, SettingsController
│   │   ├── WorkerController
│   │   └── Api/DeviceApiController
│   ├── Views/
│   │   ├── Account/      Login
│   │   ├── Admin/        Dashboard
│   │   ├── Template/     Index, Create (wizard), Edit, Details
│   │   ├── Product/      Index, Create, Edit, Details
│   │   ├── Bm/           Upload, Preview, Workload, ColourMap
│   │   ├── Report/       Sessions, Traceability, Summary, Workers, Devices, Export
│   │   ├── Device/       Index, Create, Details
│   │   ├── Worker/       JobBoard, Execute, Summary, History, Profile
│   │   └── Shared/       _Layout, _AdminLayout, _WorkerLayout, partials
│   ├── Hubs/             LiveReadingHub
│   ├── ViewModels/       Per-screen view models (never entities)
│   ├── Infrastructure/   Filters, middleware, auth handlers, DI extensions
│   └── wwwroot/          css (theme.css), js, lib (vendored), uploads (template images)
├── SmartTorque.Core             Entities, Enums, Interfaces, DTOs, domain exceptions (no dependencies on other projects)
├── SmartTorque.Services         Business logic (references Core only)
├── SmartTorque.Data             EF Core DbContext, configurations, migrations, repositories (references Core)
├── SmartTorque.Tools.Simulator  Console device simulator (torque + Vernier)
├── firmware/                    ESP32 firmware (separate folder; contract in section 12)
└── SmartTorque.Tests            xUnit unit + integration tests
```

### Dependency rule (strict)
```
Web ──▶ Services ──▶ Core
Web ──▶ Data     ──▶ Core
Services ──▶ Core  (talks to data only through interfaces declared in Core)
```
- `Core` references nothing.
- `Services` never references `Data` or `Web`. It uses repository/unit-of-work interfaces from `Core`.
- `Data` implements those interfaces.
- `Web` wires everything in DI (`Program.cs`) and holds controllers, views, hubs.

### NuGet packages
| Purpose | Package |
|---|---|
| ORM | `Microsoft.EntityFrameworkCore.SqlServer` (+ `.Tools`, `.Design`) |
| Auth | ASP.NET Core Identity (cookie auth, role-based) |
| Excel import | `ClosedXML` (reads cell fill colours; use `EPPlus` only if licensing allows) |
| Excel export | `ClosedXML` |
| PDF export | `QuestPDF` |
| Real-time | `Microsoft.AspNetCore.SignalR` (built in) |
| Logging | `Serilog.AspNetCore`, `Serilog.Sinks.File`, `Serilog.Sinks.MSSqlServer` (optional) |
| Validation | `FluentValidation.AspNetCore` (optional) or DataAnnotations |
| Testing | `xunit`, `FluentAssertions`, `Microsoft.EntityFrameworkCore.InMemory` or Testcontainers/LocalDB for integration, `Microsoft.AspNetCore.Mvc.Testing` |

Front end: Bootstrap 5 (vendored in `wwwroot/lib`, no CDN at runtime), vanilla JS or small modules, `@microsoft/signalr` client (vendored).

---

## 4. Layer Responsibilities

| Layer | Responsibility | Must not |
|---|---|---|
| **Controller** | Bind and validate input, authorize by role, call one service, return a View/JSON/redirect | Contain business rules, touch `DbContext`, build SQL, loop over entities |
| **ViewModel** | Shape data for a screen, carry validation attributes | Be an EF entity |
| **View (Razor)** | Render | Contain business logic or hard-coded colours |
| **Service** | Business rules: tolerance, priority, BM import, assignment, session logic | Reference HTTP types or EF directly |
| **Repository / Data** | Queries, persistence, transactions | Contain business decisions |
| **Hub** | Push live updates to browsers | Persist results |

### Key services
| Service | Responsibility |
|---|---|
| `ToleranceService` / `IMeasurementEvaluator` | Compute band and PASS/FAIL |
| `TemplateService` | CRUD, validation, versioning, image handling |
| `ProductService` | Product builder, cloning |
| `BmImportService` | Parse Excel, detect colour, map priority, preview, confirm |
| `PriorityService` | Priority ↔ colour map, ordering, refresh on re-upload |
| `JobAssignmentService` | Manual/auto assignment, job locking, workload balance |
| `SessionService` | Start/resume/complete session, step progression, retries, final result |
| `ReadingProcessor` | Route a device reading to the right open session and step, accept/evaluate/persist, publish live event |
| `DeviceService` | Register, API key issue/verify, heartbeat, online status |
| `ReportService` | Queries for reports, Excel/PDF export |
| `AuditService` | Write audit entries (via SaveChanges interceptor) |
| `NotificationService` | Black-job notices (SignalR group `workers`) |

---

## 5. Domain Model and Database (MS SQL Server)

### 5.1 Entity relationship overview
```
Users ──< Jobs >── Products ──< ProductComponents >── Templates ──< TemplateSteps
  │          │                                              │
  │          └──< Sessions ──< SessionResults >─────────────┘
  │                  │
  └──────────────────┴── Devices
BmUploads ──< BmRows ──> Jobs
```

### 5.2 Conventions
- PKs: `INT IDENTITY`, except `Sessions` and `SessionResults` which are `BIGINT IDENTITY`.
- Timestamps: `DATETIME2`, **UTC**.
- Measurements: `DECIMAL(10,3)`. Never `float` or `double`.
- Strings: `NVARCHAR` with explicit length.
- Soft delete via `IsActive` for config entities. **Never** delete sessions or results.
- Concurrency: `ROWVERSION` on `Jobs` (and `Templates`).
- Every config table has `CreatedBy`, `CreatedAt`, `UpdatedBy`, `UpdatedAt`.
- Use enums stored as `TINYINT` with C# enums mapped by EF value conversion.

### 5.3 Tables

**Identity:** `AspNetUsers` (extend with `FullName`, `EmployeeNo`, `IsActive`), `AspNetRoles`, `AspNetUserRoles` and the other standard Identity tables.

**Templates**
| Column | Type | Notes |
|---|---|---|
| TemplateId | INT PK IDENTITY | |
| TemplateCode | NVARCHAR(50) | Unique **together with Version** |
| Version | INT | Starts at 1 |
| TemplateName | NVARCHAR(150) | |
| Description | NVARCHAR(500) NULL | |
| TemplateMode | TINYINT | 1 Torque, 2 Vernier, 3 Mixed |
| ImagePath | NVARCHAR(300) | Relative path under `uploads/templates/` |
| StepCount | INT | Derived, kept in sync by the service |
| IsActive | BIT | |
| IsLatest | BIT | True for the newest version of a code |
| RowVersion | ROWVERSION | |
| CreatedBy / CreatedAt / UpdatedBy / UpdatedAt | | |

Unique index: `(TemplateCode, Version)`. Filtered unique index: one `IsLatest = 1` per `TemplateCode`.

**TemplateSteps**
| Column | Type | Notes |
|---|---|---|
| StepId | INT PK IDENTITY | |
| TemplateId | INT FK | |
| InstrumentType | TINYINT | 1 Torque, 2 Vernier, **per step** |
| SequenceNo | INT | Unique within template |
| Label | NVARCHAR(100) | |
| TargetValue | DECIMAL(10,3) | > 0 |
| TolerancePlus | DECIMAL(10,3) | ≥ 0 |
| ToleranceMinus | DECIMAL(10,3) | ≥ 0 |
| ToleranceMode | TINYINT | 1 absolute, 2 percent |
| Unit | NVARCHAR(10) | `N·m` or `mm` (derived from instrument) |
| MarkerX / MarkerY | DECIMAL(5,2) NULL | Position on image, % |
| MaxRetries | INT | Default from settings |

**Products**
| Column | Type |
|---|---|
| ProductId INT PK, ProductCode NVARCHAR(50) UNIQUE, ProductName NVARCHAR(150), Description NVARCHAR(500) NULL, ImagePath NVARCHAR(300) NULL, IsActive BIT, CreatedBy, CreatedAt, UpdatedBy, UpdatedAt | |

**ProductComponents**
| Column | Type | Notes |
|---|---|---|
| ProductComponentId | INT PK | |
| ProductId | INT FK | |
| TemplateId | INT FK | The exact template **version** used |
| ComponentName | NVARCHAR(100) | e.g. Left Panel |
| Quantity | INT | ≥ 1 |
| AssemblyOrder | INT | |

**BmUploads**
| Column | Type |
|---|---|
| UploadId INT PK, FileName NVARCHAR(260), StoredPath NVARCHAR(300), UploadedBy NVARCHAR(450), UploadedAt DATETIME2, Status TINYINT (Uploaded / Previewed / Confirmed / Failed), RowCount INT, ErrorCount INT | |

**BmRows**
| Column | Type | Notes |
|---|---|---|
| RowId | INT PK | |
| UploadId | INT FK | |
| ExcelRowNo | INT | For error messages |
| ProductCode | NVARCHAR(50) | |
| Quantity | INT | |
| DueDate | DATE NULL | |
| ColourHex | NVARCHAR(9) | Detected fill, `#RRGGBB` |
| Priority | TINYINT | Mapped; Admin can override in preview |
| Remarks | NVARCHAR(500) NULL | |
| ImportStatus | TINYINT | Ok / Warning / Error / Imported / Skipped |
| ImportMessage | NVARCHAR(300) NULL | |

**PriorityColourMap**
| Column | Type | Notes |
|---|---|---|
| MapId | INT PK | |
| ColourHex | NVARCHAR(7) | e.g. `#000000` |
| Priority | TINYINT | 1–5 |
| Label | NVARCHAR(30) | Black / Red / Yellow / Green / White |
| MatchTolerance | INT | RGB distance allowed, default 40 |

Seed: `#000000→1 Black`, `#FF0000→2 Red`, `#FFFF00→3 Yellow`, `#00B050→4 Green`, `#FFFFFF→5 White`. Extra shades can be added by Admin.

**Jobs**
| Column | Type | Notes |
|---|---|---|
| JobId | INT PK | |
| JobNo | NVARCHAR(30) UNIQUE | `JOB-yyyyMMdd-####` |
| ProductId | INT FK | |
| ProductSerial | NVARCHAR(60) NULL | Optional; per unit serials in `JobUnits` if enabled |
| Quantity | INT | From BM row, default 1 |
| QuantityCompleted | INT | Units fully passed |
| Priority | TINYINT | 1 Black … 5 White |
| DueDate | DATE NULL | |
| AssignmentMode | TINYINT | 1 Manual, 2 Auto |
| AssignedWorkerId | NVARCHAR(450) NULL | |
| Status | TINYINT | 1 Pending, 2 Assigned, 3 InProgress, 4 Completed, 5 OnHold, 6 Rework, 7 Cancelled |
| ExecutionMode | TINYINT | 1 Manual, 2 PLC (future, unused) |
| SourceBmRowId | INT NULL | |
| CreatedAt / UpdatedAt | DATETIME2 | |
| RowVersion | ROWVERSION | Optimistic lock for "take job" |

**Devices**
| Column | Type |
|---|---|
| DeviceId INT PK, TransmitterId NVARCHAR(50) UNIQUE, Name NVARCHAR(100), InstrumentType TINYINT, Location NVARCHAR(100) NULL, FirmwareVersion NVARCHAR(30) NULL, ApiKeyHash NVARCHAR(200), LastSeenAt DATETIME2 NULL, CalibrationDue DATE NULL, IsActive BIT | |

**Sessions**
| Column | Type | Notes |
|---|---|---|
| SessionId | BIGINT PK | |
| JobId | INT FK | |
| ProductComponentId | INT FK | |
| UnitNo | INT | 1..Job.Quantity |
| ComponentInstanceNo | INT | 1..ProductComponent.Quantity |
| WorkerId | NVARCHAR(450) | |
| TorqueDeviceId | INT FK NULL | |
| VernierDeviceId | INT FK NULL | |
| StartTime | DATETIME2 | |
| EndTime | DATETIME2 NULL | |
| Status | TINYINT | InProgress / Completed / Abandoned |
| FinalResult | TINYINT | Pass / Fail / Incomplete |
| CurrentStepSequence | INT | For resume |
| ReworkCount | INT | |
| Remarks | NVARCHAR(500) NULL | |

Unique filtered index: only one `InProgress` session per `(JobId, ProductComponentId, UnitNo, ComponentInstanceNo)`.

**SessionResults** (append only)
| Column | Type | Notes |
|---|---|---|
| ResultId | BIGINT PK | |
| SessionId | BIGINT FK | |
| StepId | INT FK | |
| InstrumentType | TINYINT | Snapshot |
| DeviceId | INT FK NULL | Transmitter that produced the value (NULL only for manual entry) |
| AttemptNo | INT | 1-based per step |
| TargetValue | DECIMAL(10,3) | Snapshot |
| TolerancePlus | DECIMAL(10,3) | Snapshot |
| ToleranceMinus | DECIMAL(10,3) | Snapshot |
| ToleranceMode | TINYINT | Snapshot |
| LowerLimit / UpperLimit | DECIMAL(10,3) | Snapshot of computed band |
| ActualValue | DECIMAL(10,3) | |
| Result | TINYINT | 1 Pass, 2 Fail |
| IsManualEntry | BIT | |
| ManualApprovedBy | NVARCHAR(450) NULL | |
| ReasonCode | NVARCHAR(30) NULL | Rework/retry reason |
| DeviceSeq | BIGINT NULL | Device sequence number for dedupe |
| CapturedAt | DATETIME2 | Device time (UTC) |
| ReceivedAt | DATETIME2 | Server time (UTC) |

Unique filtered index `(DeviceId, DeviceSeq)` where `DeviceSeq IS NOT NULL` for idempotency.

**Settings** (key/value)
`MaxRetriesDefault` (3), `StableHoldMs` (800), `ImageMaxMb` (5), `DeviceOfflineSeconds` (30), `SessionIdleTimeoutMin` (60), `BmColourMatchTolerance` (40), `BmHeaderMap` (JSON), `RequireManualEntryApproval` (true).

**AuditLog**
`LogId BIGINT`, `UserId`, `Action`, `Entity`, `EntityId`, `OldValue (NVARCHAR(MAX) JSON)`, `NewValue`, `At DATETIME2`.

**Notifications** (optional)
`NotificationId`, `Type`, `Message`, `JobId NULL`, `CreatedAt`, `ReadAt NULL`, `TargetRole`.

### 5.4 Recommended indexes
| Index | Why |
|---|---|
| `Jobs(Status, Priority, DueDate)` | Job board ordering |
| `Jobs(AssignedWorkerId, Status)` | "My jobs" |
| `Sessions(TorqueDeviceId, StartTime)` and `Sessions(VernierDeviceId, StartTime)` | Device reports; routing readings to open sessions |
| `Sessions(WorkerId, StartTime)` | Worker history |
| `SessionResults(SessionId, StepId, AttemptNo)` | Resume and report |
| `Devices(TransmitterId)` unique | Reading ingestion |
| `BmRows(UploadId)` | Preview |

### 5.5 Traceability rule
`SessionResults` stores a **snapshot** of target, tolerance and computed limits. Reports must read the snapshot, never join back to `TemplateSteps` for values. Result rows are append-only: no UPDATE and no DELETE from application code (enforce with a repository that exposes only `Add`, and optionally with a DB trigger or permissions).

---

## 6. Job and Session Model

The spec's tables imply: **a Job references a Product, and each Session runs one Component of that product.** This is resolved as follows.

- A Job has `Quantity = N` units (from the BM row).
- A Product has components `C1..Cm`, each with `ProductComponent.Quantity = q_i`.
- The Job needs `N × Σ q_i` sessions in total, one per `(UnitNo, ProductComponentId, ComponentInstanceNo)`.
- A session covers **all steps of that component's template**, in sequence.
- The Worker Execute screen shows a **component picker** (list of remaining sessions for the job). The worker starts one, finishes it, returns to the picker, and continues. A worker may also resume an `InProgress` session.
- **Job completes** when every required session has `FinalResult = Pass`. `Job.QuantityCompleted` increments when all sessions of one unit pass.
- A session that fails after retries sets `FinalResult = Fail`, `Job.Status = Rework`, and a new session (new attempt for that component) can be started after rework approval; `ReworkCount` increments.

### 6.1 Job state machine
```
Pending ──assign──▶ Assigned ──start──▶ InProgress ──all sessions pass──▶ Completed
   ▲                    │                   │  │
   │                    └──unassign─────────┘  ├── hold ──▶ OnHold ──resume──▶ InProgress
   └────────────────────────────────────────── └── retries exhausted ──▶ Rework ──approve──▶ InProgress
Any non-completed state ──cancel (Admin)──▶ Cancelled
```
Rules: only Admin cancels/holds; a worker can only start a job that is `Pending` (open) or `Assigned` to them; "start" uses an optimistic update on `RowVersion` so only one worker wins.

### 6.2 Session state machine
```
InProgress ──all steps final PASS, worker Submit──▶ Completed (FinalResult = Pass)
InProgress ──a step exhausts retries──▶ Completed (FinalResult = Fail) + Job → Rework
InProgress ──idle > SessionIdleTimeoutMin or admin abandon──▶ Abandoned (FinalResult = Incomplete)
```
A browser close does **not** change state. Resume reads `CurrentStepSequence` and the saved results.

---

## 7. Core Algorithms

### 7.1 Tolerance evaluation (`IMeasurementEvaluator`)
```
if mode == Absolute:
    lower = target - toleranceMinus
    upper = target + tolerancePlus
else (Percent):
    lower = target * (1 - toleranceMinus / 100)
    upper = target * (1 + tolerancePlus / 100)

result = (lower <= actual && actual <= upper) ? Pass : Fail     // inclusive bounds
```
Use `decimal` throughout. Round limits and actual to 3 decimals using `MidpointRounding.AwayFromZero` before comparing. Return `{Result, Lower, Upper, Delta = actual - target}`. Unit-test the boundaries (exactly lower, exactly upper, just outside, zero tolerance, percent mode).

### 7.2 Priority and ordering
```
OrderBy(Priority).ThenBy(DueDate ?? DateTime.MaxValue).ThenBy(CreatedAt)
```
Implement once in `PriorityService` / a shared query extension and reuse on the worker board and the workload board.

### 7.3 BM import
1. Upload `.xlsx` (validate extension, size ≤ 10 MB, MIME). Save under `uploads/bm/{guid}.xlsx`. Create `BmUploads` row.
2. Read the first worksheet (configurable). Header row is row 1 by default. Map headers to fields using `BmHeaderMap` (case-insensitive, trimmed): `ProductCode`, `Quantity`, `DueDate`, `Remarks`.
3. For each non-empty row:
   - Parse `ProductCode`, `Quantity` (int > 0), `DueDate` (date; accept Excel serial or text), `Remarks`.
   - **Colour detection:** read the fill of the **ProductCode cell**; if no fill, check the other cells in the row; if still none, treat as White. Resolve the colour:
     - `Fill.PatternType == None` or solid white → `#FFFFFF`.
     - RGB colour → hex.
     - **Theme colour** → resolve through the workbook theme and apply the `Tint`.
     - **Indexed colour** → resolve through the indexed palette.
   - **Match** to `PriorityColourMap` by nearest RGB (Euclidean distance) within `MatchTolerance`. No match → `ImportStatus = Error` ("unrecognised colour") and the Admin must choose a priority in the preview or skip the row.
   - Verify `ProductCode` exists and is active → else `Error`.
4. Show the **preview** (all rows with status). Admin can fix priority, skip rows, then **Confirm**.
5. On confirm, in one transaction: create a `Job` per imported row (`JobNo` generated, `Quantity`, `Priority`, `DueDate`, `SourceBmRowId`). Mode Auto: status `Pending` and published to the board. Mode Manual: `Pending` until assigned.
6. **Re-upload rule:** match existing **open** jobs (status Pending/Assigned/OnHold) by `(ProductCode, DueDate)`; update `Priority`/`Quantity` where changed. Do not touch `InProgress`/`Completed`/`Cancelled`. Rows absent from the new sheet are not auto-cancelled; list them as "not in latest BM" for the Admin.
7. If any new Black job is created, call `NotificationService` to notify workers.

Treat the colour detection as the highest-risk parser piece: test with a real sample sheet, including theme colours and pure black.

### 7.4 Job assignment
- **Manual:** Admin picks a worker or leaves the job open.
- **Auto:** after import, jobs are published `Pending`. Optional balancing: assign to the worker with the fewest `Assigned` + `InProgress` jobs (ties by last activity).
- **Start** (worker): `UPDATE Jobs SET Status=InProgress, AssignedWorkerId=@w WHERE JobId=@id AND RowVersion=@rv AND Status IN (Pending, Assigned-to-me)`. If 0 rows are updated, show "Job already taken".

### 7.5 Reading acceptance (`ReadingProcessor`)
```
POST /api/device/reading
 1. Authenticate device by X-Api-Key → Device (active) else 401/403.
 2. Update Device.LastSeenAt.
 3. Dedupe by (DeviceId, seq) if seq present → return 200 {duplicate:true}.
 4. Find the open session bound to this device (TorqueDeviceId or VernierDeviceId = device) with Status InProgress.
    None → 202 {accepted:false, reason:"NO_ACTIVE_SESSION"}  (live value still not shown).
 5. Get the session's current step (CurrentStepSequence).
 6. If step.InstrumentType != device.InstrumentType → 409 {reason:"UNEXPECTED_INSTRUMENT"}; broadcast a hint to the browser; do not save.
 7. Broadcast liveValue (always, for non-stable and stable readings) to SignalR group session-{id}.
 8. If !reading.IsStable → return 200 {accepted:false}.
 9. Evaluate with IMeasurementEvaluator using the step's current target/tolerance.
10. In one transaction: insert SessionResult (snapshot, AttemptNo = previous attempts + 1);
    on Pass → advance CurrentStepSequence (or mark "ready to submit" when last step);
    on Fail → if attempts < MaxRetries: stay on the step; else: mark session Fail, job → Rework.
11. Broadcast stepResult and stepChanged/sessionReady/sessionFailed events.
12. Return 200 with {accepted:true, result:"Pass|Fail", lower, upper}.
```
Concurrency: per-session lock (e.g. `SemaphoreSlim` keyed by session ID, or a transaction with `UPDLOCK` on the Session row) so two near-simultaneous readings cannot both advance the step.

### 7.6 Sequence enforcement
Steps are presented strictly in `SequenceNo` order. The server rejects results for any step other than the current one. (Out-of-order is not a feature to relax unless the owner asks.)

### 7.7 Stable value rules
- **Torque:** the device sends `isStable = true` with the **peak** value when the wrench signals release or the value holds within a small band for `StableHoldMs`. The server trusts `isStable`; it never invents stability from a stream.
- **Vernier:** `isStable = true` when the caliper's data/hold button is pressed or the value holds for `StableHoldMs`.
- Configurable per instrument in `Settings`. The decision is open (see `PRD.md` section 10, risk 3).

---

## 8. Device API (ASP.NET Core)

Base path `/api/device`. All requests carry header `X-Api-Key: <key>`; the transmitter ID is also in the body/route and must match the key's device.

| Endpoint | Method | Purpose |
|---|---|---|
| `/api/device/register` | POST | Device announces itself `{transmitterId, firmware, instrument}`; updates firmware/last seen; does not create devices (Admin does) |
| `/api/device/heartbeat` | POST | Keep-alive every ~10 s; sets `LastSeenAt` |
| `/api/device/reading` | POST | Send a reading (below) |
| `/api/device/session/{transmitterId}` | GET | Current step the device is expected to serve (label, target, unit, instrument) or `204` when none |
| `/hubs/live` | WebSocket (SignalR) | Live value stream to the worker's browser |

### 8.1 Reading request
```json
{
  "transmitterId": "ESP32-TW-001",
  "instrument": "Torque",
  "value": 24.8,
  "unit": "Nm",
  "isStable": true,
  "seq": 10231,
  "timestamp": "2026-10-12T09:15:30Z"
}
```
Validation: `transmitterId` matches the key's device; `instrument` matches the device's registered type; `value` is a finite number; `unit` is `Nm`/`N·m` for torque or `mm` for Vernier (reject others; conversion happens in firmware); `timestamp` within a sane window (accept offline-buffered older timestamps, reject future > 5 min).

### 8.2 Reading response
```json
{ "accepted": true, "duplicate": false, "result": "Pass", "lower": 24.0, "upper": 26.0, "step": 3, "next": 4 }
```
Status codes: `200` processed; `202` received but no active session; `400` invalid body; `401` bad key; `403` inactive device or key/transmitter mismatch; `409` unexpected instrument; `429` rate limited.

### 8.3 Live streaming from the device
Default: the device POSTs readings at about 5–10 Hz with `isStable:false` while measuring, then one `isStable:true` reading. The server rebroadcasts through SignalR. An optional later optimisation is a raw WebSocket from the ESP32 to a `/ws/device` endpoint using the same payload; the contract does not change.

### 8.4 Security
- Per-device API key: 32+ random bytes, shown once at registration, stored as a hash (e.g. SHA-256 with a pepper, or PBKDF2).
- HTTPS where possible; on a closed LAN HTTP is tolerated but keys must still be sent only in headers.
- Reject unregistered/inactive devices; rate limit per device (e.g. 20 requests/second).
- Never log API keys.

---

## 9. SignalR Contract (`/hubs/live`, `LiveReadingHub`)

Authenticated (cookie) browsers only. Groups:
- `session-{sessionId}` for a worker's active session (joined via `JoinSession(sessionId)` after server checks that the session belongs to the user).
- `workers` (all workers: urgent job notices).
- `admins` (device online/offline, rework alerts, dashboard).

| Direction | Event | Payload |
|---|---|---|
| client → server | `JoinSession(sessionId)` | Validates ownership |
| client → server | `LeaveSession(sessionId)` | |
| server → client | `liveValue` | `{sessionId, stepId, instrument, value, unit, isStable, inTolerance, lower, upper, ts}` |
| server → client | `stepResult` | `{sessionId, stepId, attemptNo, actual, result, lower, upper}` |
| server → client | `stepChanged` | `{sessionId, stepSequence, stepId, instrument, label, target, tolerancePlus, toleranceMinus, unit, markerX, markerY, totalSteps}` |
| server → client | `sessionReady` | `{sessionId}` (all steps pass; Submit enabled) |
| server → client | `sessionFailed` | `{sessionId, reason}` |
| server → client | `deviceStatus` | `{transmitterId, online}` |
| server → client | `urgentJob` | `{jobId, jobNo, priority}` |
| server → client | `hint` | `{code, message}` (e.g. UNEXPECTED_INSTRUMENT, DEVICE_OFFLINE) |

`inTolerance` in `liveValue` is for the live colour only; the stored result still comes from the server evaluation on the stable reading.

A page reload re-joins the session group and the page loads current state (current step, last results) from the server, then listens for events.

---

## 10. Key Sequences

### 10.1 Worker executes a mixed component
```
Worker      Browser           Server                        ESP32 (torque)    ESP32 (vernier)
  │ Start ────▶│ POST /Worker/Start        │                      │                 │
  │            │──────────────────────────▶│ lock job, create session, bind devices │
  │            │◀── Execute page (step 1 torque) ───────────────  │                 │
  │ tighten ───────────────────────────────────────────────────▶ │                 │
  │            │                           │◀── POST reading (live, !stable) ───────│
  │            │◀── liveValue ─────────────│                      │                 │
  │            │                           │◀── POST reading (stable) ──────────────│
  │            │                           │ evaluate, save result, advance step    │
  │            │◀── stepResult + stepChanged (step 9 is Vernier) ─│                 │
  │ measure ────────────────────────────────────────────────────────────────────────▶ │
  │            │                           │◀── POST reading (stable, vernier) ─────────────────┤
  │            │◀── stepResult + sessionReady ─────────────────── │                 │
  │ Submit ───▶│ POST /Worker/Submit       │ close session, update job status       │
```

### 10.2 BM upload
```
Admin ─upload─▶ BmController ─▶ BmImportService.Parse ─▶ preview (BmRows, status per row)
Admin ─fix/skip/confirm─▶ BmImportService.Confirm ─▶ create Jobs (transaction) ─▶ NotificationService (if Black)
```

---

## 11. Template Versioning Algorithm

```
SaveTemplate(edit):
  if template has never been used by any Session or ProductComponent in Active use:
      update in place
  else:
      create new Template row: same TemplateCode, Version = max + 1, copy steps with edits,
      set old.IsLatest = false, new.IsLatest = true
      (Products keep their old template version until an Admin updates the component to the new version)
```
"Used" = referenced by any `Session` through `ProductComponent`. Product builder lists **latest** versions by default and shows the version number.

---

## 12. ESP32 Gateway Firmware Contract

Firmware lives in `firmware/` and is built after the USB protocol is confirmed (Phase 6). The web app only depends on this contract.

- **Hardware:** ESP32-S2/S3 (USB-OTG host) or a standard ESP32 plus a USB host module. Two instrument channels (two ESP32s, or one with two channels) each have a unique `TransmitterId`.
- **USB side:** parse the instrument's output (HID keyboard emulation, USB-serial/CDC, or vendor protocol, to be confirmed) into `{value, unit, isStable}`.
- **Config (stored in flash):** Wi-Fi SSID/password, server base URL, `TransmitterId`, API key, instrument type.
- **Behaviour:**
  1. Connect to Wi-Fi, `POST /register`, then `POST /heartbeat` every ~10 s.
  2. While a measurement runs, POST live samples (`isStable:false`) at 5–10 Hz; POST the final value with `isStable:true`.
  3. Increment a persistent `seq` for every reading.
  4. **Offline buffer:** if a POST fails, store stable readings in flash/RAM (ring buffer) and replay in order with their original `seq` and timestamp when Wi-Fi returns. Live samples are dropped, not buffered.
  5. Convert units to N·m / mm before sending.
  6. Use NTP for UTC timestamps; if NTP is unavailable, send `timestamp` empty and the server uses `ReceivedAt`.
- **Simulator:** `SmartTorque.Tools.Simulator` sends the same payloads (torque ramp to a peak, Vernier steady values) so the web app can be developed and tested with no hardware.

---

## 13. Security

| Concern | Measure |
|---|---|
| AuthN | Identity cookie auth, secure/HttpOnly/SameSite=Lax, sliding expiry, lockout on repeated failures |
| AuthZ | Role attributes on controllers and actions; ownership checks (a worker can access only own sessions); default deny with a fallback policy requiring authentication |
| CSRF | `[ValidateAntiForgeryToken]` globally via `AutoValidateAntiforgeryTokenAttribute`; device API is exempt and uses API keys |
| Input | Server-side validation on every model; never trust client-side checks |
| Uploads | Allow-list extension (`.jpg`, `.jpeg`, `.png`, `.xlsx`), check magic bytes, enforce size, store under generated GUID names outside executable paths |
| Secrets | `appsettings` has no secrets; use user-secrets / environment variables / protected config |
| SQL | EF parameterised queries only |
| Transport | HTTPS + HSTS in production when a certificate is available |
| Rate limiting | `Microsoft.AspNetCore.RateLimiting` on `/api/device/*` and login |
| Logging | Never log passwords, API keys or full tokens |

---

## 14. Configuration

`appsettings.json` (non-secret) and environment-specific overrides:
```json
{
  "ConnectionStrings": { "Default": "<from user-secrets or env>" },
  "Storage": { "UploadRoot": "wwwroot/uploads" },
  "Device": { "OfflineSeconds": 30, "RateLimitPerSecond": 20 },
  "Session": { "IdleTimeoutMinutes": 60, "MaxRetriesDefault": 3 },
  "Features": { "SimulatorEnabled": false, "ManualEntryFallback": false },
  "Display": { "TimeZoneId": "India Standard Time" }
}
```
`Features:SimulatorEnabled` is `true` only in Development.

---

## 15. Cross-Cutting Concerns

- **Logging:** Serilog to rolling files; structured properties for `SessionId`, `JobId`, `TransmitterId`, `UserId`. Warnings for rejected readings; errors with exception details.
- **Errors:** one global exception handler; friendly error page for Razor; JSON problem details for `/api/*`.
- **Transactions:** every multi-row write (BM confirm, reading accept, session complete) is one transaction.
- **Audit:** an EF `SaveChangesInterceptor` writes `AuditLog` rows for Templates, TemplateSteps, Products, ProductComponents, PriorityColourMap, Devices, Settings, Users. Session results are not audited because they are already immutable.
- **Time:** services take `TimeProvider`/`IClock` so tests control time.
- **Localisation:** English only for v1; keep strings in resource-friendly places if cheap.

---

## 16. Deployment

- Host: IIS (ASP.NET Core Hosting Bundle) or Kestrel as a Windows service/systemd unit on a local server.
- Database: SQL Server on the same LAN; EF migrations applied by an explicit step (`dotnet ef database update` or a migration bundle), not automatically in production.
- Network: ESP32 devices and worker tablets on the same Wi-Fi/LAN as the server; WebSockets enabled in IIS.
- No internet dependency: all front-end libraries are vendored; no CDN, no external fonts at runtime.
- Backups: daily full SQL backup with retention; uploads folder backed up with it.
- Health: `/health` endpoint (DB reachable, disk space).

---

## 17. Testing Architecture

| Level | Scope | Tools |
|---|---|---|
| Unit | `ToleranceService`, priority mapping and ordering, BM colour matching and parser (with sample xlsx fixtures), session/job state transitions, versioning rules | xUnit, FluentAssertions |
| Integration | Device API (accept, duplicate, wrong instrument, no session, bad key), session lifecycle, EF queries, role authorization | `WebApplicationFactory`, LocalDB/Testcontainers |
| Simulator | End-to-end torque and Vernier flows against a running instance | `SmartTorque.Tools.Simulator` |
| UAT | One real product (6-panel locker) end to end | Manual |

Each service has tests before its controller is considered done. See `Rules.md` for the test gate.
