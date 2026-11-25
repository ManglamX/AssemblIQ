# AssemblIQ (SmartTorque) — Comprehensive Bug Audit, Security Review & Code Quality Report

**Audit Date:** 2026-11-25  
**Target Solution:** `SmartTorque/SmartTorque.sln`  
**Technology Stack:** `.NET 9.0` (`net9.0`), ASP.NET Core MVC + Areas (`Admin`, `Worker`) + Web API, EF Core `9.0.13` (SQL Server), ASP.NET Core Identity + Cookie Auth, SignalR, Serilog, ClosedXML `0.102.3`, QuestPDF `2025.1.0`.

---

## 1. Projects & Modules Inspected
- `SmartTorque.Core` — Domain entities (`Device`, `BleProtocol`, `Job`, `JobUnit`, `Session`, `SessionResult`, `ApplicationUser`, `Product`, `Template`, `ApiKeyDirectoryEntry`, etc.), enums, interfaces.
- `SmartTorque.Data` — `AppDbContext`, EF Core migrations, `DataSeeder`.
- `SmartTorque.Services` — `SessionService`, `DeviceService`, `BmPreviewService`, `BmImportService`, `ImageUploadService`, `ReportService`, `ApiKeyDirectoryService`, `HierarchyService`, `AssemblyService`, etc.
- `SmartTorque.Web` — `Program.cs`, `ServiceCollectionExtensions.cs`, `ApplicationServiceExtensions.cs`, 20 MVC controllers, 2 API controllers (`ReadingsApiController`, `DeviceApiController`), `LiveReadingHub`, `DeviceHeartbeatWatchdogService`, `TelemetryIngestionWorker`, `TelemetryIngestionChannel`, `tohnichi-ble-driver.js`.
- `SmartTorque.Tests` — `408` xUnit tests.
- `SmartTorque.Tools.Simulator` — Hardware simulator tool.

---

## 2. Confirmed Findings & Remediation Status

| # | Severity | Finding | File / Symbol | Status |
|---|---|---|---|---|
| 1 | 🔴 Critical | Unauthenticated hardware API key QR disclosure (`[AllowAnonymous]` on `QrCode/{id}` & `RawKeyQr`) | `DeviceController.cs` | **Fixed** (removed `[AllowAnonymous]`, added department check on `QrCode`, clamped `pixels`) |
| 2 | 🔴 Critical | Unauthenticated active session/worker/job exposure (`[AllowAnonymous]` on `Simulator`) | `JobBoardController.cs` | **Fixed** (removed `[AllowAnonymous]`) |
| 3 | 🔴 Critical | Missing `Admin \| IsLineSupervisor` authorization on `AllocateProductPackage`, `AllocateJob`, `DeallocateJob` | `SupervisorController.cs` | **Fixed** (enforced `IsAdmin \|\| IsLineSupervisor` + department checks) |
| 4 | 🟠 High | Missing `SessionId` ownership check in `SubmitReading` & cross-plant fallback in `ProcessReadingAsync` | `JobBoardController.cs`, `SessionService.cs` | **Fixed** in `SubmitReading` (verifies `session.WorkerId == currentUserId` unless `Admin`); fallback in `ProcessReadingAsync` remaining for hardware `/api/readings` |
| 5 | 🟠 High | Unauthenticated device directory lookup (`[AllowAnonymous]` on `LookupDeviceByQr`) | `JobBoardController.cs` | **Fixed** (removed `[AllowAnonymous]`) |
| 6 | 🟠 High | Auto-registered BLE devices created with `DepartmentId = null` | `SessionService.cs` | **Fixed** (inherits `DepartmentId` from `session.Job.Product` / `AssemblyLine`) |
| 7 | 🟠 High | Missing DB transaction & concurrency token on `Session` in `ProcessReadingAsync` | `SessionService.cs` | **Remaining** (needs `Session.RowVersion` + transaction) |
| 8 | 🟡 Medium | Unbounded `pixels` parameter on QR SVG endpoints | `DeviceController.cs`, `JobController.cs` | **Fixed** (`Math.Clamp(pixels ?? 10, 1, 25)` and removed `[AllowAnonymous]` on `UnitQrCode`) |
| 9 | 🟡 Medium | Missing `[EnableRateLimiting("device-rate-limit")]` on `ReadingsApiController` | `ReadingsApiController.cs` | **Fixed** (added `[EnableRateLimiting("device-rate-limit")]`) |
| 10 | 🟡 Medium | SignalR `LiveReadingHub` lacked `[Authorize]` & `Admin` check on `JoinAdmins` | `LiveReadingHub.cs` | **Fixed** (added `[Authorize]` & `Admin` role check in `JoinAdmins`) |
| 11 | 🟡 Medium | Startup `Task.Run` catch block logged via disposed `logger` | `Program.cs` | **Fixed** (uses `Log.Warning`) |
| 12 | 🟡 Medium | `[IgnoreAntiforgeryToken]` on several internal JSON POSTs; `TelemetryIngestionWorker.ProcessBatch` is a no-op; `PendingModelChangesWarning` suppressed; `CookieSecurePolicy.SameAsRequest` | Multiple | **Remaining** (document/schedule for next hardening pass) |
| 13 | 🟡 Medium | 2 pre-existing failing unit tests (`UserApprovalTests.Register_WorkerOrSupervisorWithoutDepartment_ReturnsModelError`, `BmPreviewServiceTests.EditingIsRefusedOnceTheUploadHasBeenImported`) | `UserApprovalTests.cs`, `BmPreviewServiceTests.cs` | **Remaining** (reflects intentional product changes vs older test assertions) |

---

## 3. Supply-Chain & Verification Summary
- **NuGet Vulnerabilities (`dotnet list SmartTorque/SmartTorque.sln package --vulnerable`):** `0` vulnerable packages across all 6 projects.
- **Solution Build (`dotnet build SmartTorque/SmartTorque.sln --no-restore`):** `0 Warning(s)`, `0 Error(s)`.
- **Unit & Integration Tests (`dotnet test SmartTorque/SmartTorque.sln`):** `408` total — `404 Passed`, `2 Failed` (pre-existing), `2 Skipped` (`HierarchyMigrationRunner`).
- **Verified OK:** `ImageUploadService` (magic bytes, size, extension whitelist, sanitized subfolder, GUID filenames), parameterized EF Core queries & static-literal `ExecuteSqlRawAsync`.
