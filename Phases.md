# Phases — Delivery Order

| Item | Value |
|---|---|
| Document | Delivery plan, small chunks, in dependency order |
| Read with | `PRD.md` (requirement IDs), `Architecture.md` (how), `Rules.md` (gate), `Memory.md` (status) |
| Rule | Do tasks **in order**. Finish a task fully (build + tests + `Memory.md` update) before starting the next. Do not start a phase until the previous phase's exit criteria are met. |

**Task ID format:** `P<phase>.<n>`. Tick the box in `Memory.md`'s tracker, not here, so this file stays a clean plan.

**Sizing:** each task should fit in one focused session (roughly half a day or less). If a task grows, split it and record the split in `Memory.md`.

**Global definition of done** for every task: builds clean, tests written and passing, UI exercised at tablet width, `dotnet format` clean, `Memory.md` updated (see `Rules.md` section 9).

---

## Phase 0: Preparation (no code, or only throwaway spikes)

Goal: remove the two biggest unknowns early without blocking software work.

| Task | What | Done when |
|---|---|---|
| P0.1 | Confirm how the USB torque wrench exposes data (HID keyboard / USB-serial CDC / vendor protocol). Capture sample output on a PC. | A sample capture file and a one-page note on the data format is added to `Memory.md` |
| P0.2 | Same for the USB Vernier caliper. Record how "hold"/data button is signalled. | Same as above |
| P0.3 | Decide ESP32 model with USB host capability (ESP32-S2/S3 or external USB host shield). | Decision in `Memory.md` before hardware is ordered |
| P0.4 | Obtain a **real BM Excel sample** (with row colours) and the colour legend. | Sample stored in `SmartTorque.Tests/Fixtures/` (sanitised); header names noted |
| P0.5 | Confirm open questions: device binding rule, re-upload row key, stable vs button accept, units. | Answers logged under *Decisions* in `Memory.md` |

Exit: P0 can run **in parallel** with Phase 1 and 2. Phases 1 to 5 do not depend on it.

---

## Phase 1: Foundation

Goal: runnable solution with login, roles, theme and layout.

| Task | What | Requirement |
|---|---|---|
| P1.1 | Create the solution and projects: `Web`, `Core`, `Services`, `Data`, `Tests`, `Tools.Simulator` (empty console). Add project references per the dependency rule. Add `.editorconfig`, `.gitignore`, nullable enabled, warnings policy. | Architecture §3 |
| P1.2 | `AppDbContext`, Identity setup (`ApplicationUser` with FullName, EmployeeNo, IsActive), SQL Server connection from user-secrets, first migration. | AUTH-1 |
| P1.3 | Seed roles `Admin` and `Worker`; seed first Admin from configuration; idempotent seeding. | AUTH-2, AUTH-5 |
| P1.4 | Login/logout, lockout, anti-forgery, role-based redirect (Admin → `/Admin`, Worker → `/Worker`), fallback authorize policy, access-denied page. | AUTH-2, AUTH-4 |
| P1.5 | Theme and layouts: `theme.css` with the design tokens, `_AdminLayout` (sidebar + top bar), `_WorkerLayout` (minimal), shared partials (alerts, badges, empty state, pagination). Vendor Bootstrap 5 locally. | `Design.md` |
| P1.6 | Admin dashboard shell (placeholder cards), Worker job board shell (empty state). | DASH |
| P1.7 | User management (Admin): list, create, edit, deactivate, reset password, assign role. | AUTH-3 |
| P1.8 | Cross-cutting: Serilog, global error handling, `/health`, `TimeProvider`, settings table with seeded defaults, `ExecutionMode` enum added to Core (unused). | Architecture §15 |
| P1.9 | Test project setup with `WebApplicationFactory`; role authorization tests (Worker cannot open Admin pages, anonymous redirected). | Rules §9 |

**Exit:** Admin and Worker can log in and land on their own pages; themed layout works; tests green; README explains setup.

---

## Phase 2: Templates

Goal: Admin builds reusable, versioned templates with per-step instrument selection.

| Task | What | Requirement |
|---|---|---|
| P2.1 | Entities `Template`, `TemplateStep`; enums `InstrumentType`, `TemplateMode`, `ToleranceMode`; EF configuration; migration; unique `(TemplateCode, Version)` and one `IsLatest` per code. | TPL-1…7 |
| P2.2 | `IMeasurementEvaluator` / `ToleranceService` with unit tests for the band calculation (absolute and percent, inclusive bounds, rounding). Needed here so the preview can show the band. | Architecture §7.1 |
| P2.3 | Image upload service: JPG/PNG, ≤ 5 MB, magic-byte check, GUID filename, stored under `uploads/templates/`. | TPL-4, TPL-7 |
| P2.4 | Template wizard stage 1: Basic info (name, unique code, description, mode). | TPL-1, TPL-2, TPL-9 |
| P2.5 | Wizard stage 2: Image and number of steps; generate step rows; image preview; click-to-place marker (stored as % X/Y). | TPL-3, TPL-4, TPL-5 |
| P2.6 | Wizard stage 3: per-step instrument, label, target, tolerance plus/minus, tolerance mode, sequence order (drag or number), max retries; unit auto-derived; mode pre-fills instrument. Inline validation. | TPL-5, TPL-6, TPL-7 |
| P2.7 | Preview and Save; Template detail page showing the band per step (using the evaluator). | TPL-9, TPL-10 |
| P2.8 | Template list with search and filters (mode, instrument, active). Activate/deactivate. | TPL-10 |
| P2.9 | **Versioning:** editing a used template creates a new version; unused template edits in place; old versions remain viewable. Tests for both paths. | TPL-8 |
| P2.10 | Audit interceptor writes `AuditLog` for template changes (can land in Phase 8 if the table exists; at minimum create the table now). | Architecture §15 |

**Exit:** a template with a mix of torque and Vernier steps can be created, edited (versioned), listed and previewed. Evaluator tests pass.

---

## Phase 3: Products

Goal: compose templates into products.

| Task | What | Requirement |
|---|---|---|
| P3.1 | Entities `Product`, `ProductComponent`; migration; unique `ProductCode`. | PRD-1…5 |
| P3.2 | Product create/edit: name, code, description, optional image. | PRD-1 |
| P3.3 | Product builder: add components from latest template versions, component name, quantity, same template reusable, drag to reorder assembly order; at least one component. | PRD-2…4 |
| P3.4 | Product detail with expandable component → steps view; show template version. | PRD-5, PRD-7 |
| P3.5 | Clone product. | PRD-6 |
| P3.6 | List with search, activate/deactivate. | PRD-7 |
| P3.7 | Update-to-latest-version action per component (explicit, shows the diff of steps). | Architecture §11 |

**Exit:** the 6-panel locker product can be built from six templates and displayed.

---

## Phase 4: BM and Jobs

Goal: BM Excel becomes prioritised jobs; Black first.

| Task | What | Requirement |
|---|---|---|
| P4.1 | Entities `BmUpload`, `BmRow`, `PriorityColourMap`, `Job` (with `Quantity`, `RowVersion`, `ExecutionMode`), `Notification`; migrations; seed the colour map (Black, Red, Yellow, Green, White). | BM-3 |
| P4.2 | Colour map admin screen (add/edit/delete shades, priority, label, match tolerance). | BM-3, US-A5 |
| P4.3 | `BmImportService` parser: header mapping (configurable), row reading, quantity/date parsing, product-code lookup. Fixture-based unit tests. | BM-2 |
| P4.4 | **Colour detection** (RGB, theme + tint, indexed, no fill → White) and nearest-colour matching with tolerance. Tests for pure black, theme colours, near-white, unknown colours. Use the real sample from P0.4 when available. | BM-2, BM-3 |
| P4.5 | Upload page and **preview** screen: rows, detected colour swatch, mapped priority badge, status, messages; Admin can override priority or skip a row; nothing created yet. | BM-4 |
| P4.6 | Confirm import: create jobs in one transaction, generate `JobNo`, set status, optional product serial; fire Black-job notification. | BM-5, BM-10 |
| P4.7 | `PriorityService` ordering (Priority → DueDate → Upload time) as a reusable query; tests. | PRD §5.4 |
| P4.8 | Workload board: sort/filter by priority colour, due date, worker, status; Black pinned and highlighted. | BM-9 |
| P4.9 | Assignment: manual (pick worker or leave open, override priority) and automatic (publish ranked, optional balance by workload); unassign. | BM-7, BM-8 |
| P4.10 | **Re-upload refresh** of open jobs' priorities by `(ProductCode, DueDate)`; in-progress jobs untouched; "not in latest BM" list. Tests. | BM-6 |
| P4.11 | Job detail page and Admin actions: hold, resume, cancel. Job state machine in the service with tests. | Architecture §6.1 |

**Exit:** uploading a real BM creates jobs with correct priorities; the workload board shows Black first; re-upload behaves per rule.

---

## Phase 5: Worker Portal (with simulated readings)

Goal: complete worker flow without hardware. Readings come from a simulator page.

| Task | What | Requirement |
|---|---|---|
| P5.1 | Entities `Device`, `Session`, `SessionResult`; migrations; unique/filtered indexes (one in-progress session per unit-component, `(DeviceId, DeviceSeq)`). | RPT-1, RPT-2 |
| P5.2 | Device registration (minimal in this phase): Admin creates a device, API key generated once and stored hashed. Seed two simulated devices (torque, Vernier) in Development. | DEV-1 |
| P5.3 | Worker Job Board: ordered by the rule, filters, priority badges with labels, Black pinned; shows jobs open or assigned to the worker. | WRK-1 |
| P5.4 | **Start job:** optimistic locking on `RowVersion`, "already taken" handling, device selection (torque and/or Vernier as the job needs), session creation. | WRK-2, WRK-3 |
| P5.5 | Component picker for a job (remaining sessions per unit/component instance); resume of an in-progress session. | Architecture §6, WRK-9 |
| P5.6 | `SessionService` and `ReadingProcessor` core (accept rules, retries, step advance, final result, transaction, per-session lock). Heavy unit tests. | Architecture §7.5 |
| P5.7 | `LiveReadingHub` (`/hubs/live`), group join with ownership check, events `liveValue`, `stepResult`, `stepChanged`, `sessionReady`, `sessionFailed`, `hint`. | Architecture §9 |
| P5.8 | **Execute screen:** component name, image with highlighted marker, label, required value and band, live value (torque gauge or Vernier readout by step), step counter, instrument indicator that switches automatically, PASS/FAIL with icon + text. | WRK-4, WRK-5 |
| P5.9 | Retry and fail flow: FAIL display, retry up to `MaxRetries`, then session Fail and job → Rework, reason code captured. | WRK-7 |
| P5.10 | Submit and Summary screen; session closes with final result; job status updates per the job completion rule. | WRK-8, §5.7 |
| P5.11 | Sequence enforcement (server rejects out-of-order). | Feature 3 |
| P5.12 | **Dev simulator:** a `/dev/simulator` page (Development only) and the console `Tools.Simulator` that post torque ramps/peaks and Vernier values with `seq`, through the real device endpoint path. | Rules §8.9 |
| P5.13 | My History and Profile pages for the worker. | WRK-10 |
| P5.14 | Black-job notification to the worker board via `workers` group. | BM-10 |

**Exit:** with the simulator, a worker can take the top job, run a mixed torque + Vernier component to completion, fail and retry a step, close and reopen the browser and resume, and submit. All results are stored with snapshots.

---

## Phase 6: Device Integration

Goal: real ESP32 hardware feeds the same pipeline.

| Task | What | Requirement |
|---|---|---|
| P6.1 | Device API endpoints: `register`, `heartbeat`, `reading`, `session/{id}` with API-key auth handler, validation, status codes, rate limiting, problem details. Integration tests (accepted, duplicate, wrong instrument, no session, bad key, inactive device). | Architecture §8 |
| P6.2 | Device management UI: list with online/offline (heartbeat), last seen, instrument, location, key reissue, activate/deactivate; `deviceStatus` event to Admin. | DEV-1…3 |
| P6.3 | Refine stable rules per instrument using the real protocol from P0 (torque peak, Vernier hold/button); adjust settings (`StableHoldMs`). | Architecture §7.7 |
| P6.4 | ESP32 firmware (`firmware/`): USB host parse for the torque wrench and the caliper, Wi-Fi, register/heartbeat, live + stable posting, persistent `seq`, config in flash. | Architecture §12 |
| P6.5 | Firmware offline buffer and ordered replay with original `seq`/timestamps; NTP time. | Feature 10 |
| P6.6 | Latency test on the plant LAN: live value under 500 ms; fix bottlenecks (batching, SignalR transport). | NFR |
| P6.7 | Swap the simulator for real devices in a worker test; keep the simulator for regression. | Phase 5 exit |

**Exit:** a real tightening and a real caliper reading flow end to end; replaying buffered readings creates no duplicates.

---

## Phase 7: Reports and Dashboard

Goal: traceability and operational reporting.

| Task | What | Requirement |
|---|---|---|
| P7.1 | `ReportService` queries with indexes; common filter component (date range, worker, product, device, result). | RPT-4 |
| P7.2 | **Session report** (list + session detail with every step, attempt and device). | RPT-1, RPT-2, RPT-4 |
| P7.3 | **Traceability report:** search by product serial, job number or device ID; full history timeline. | RPT-5 |
| P7.4 | **Pass/Fail summary:** counts, first-pass yield, failure rate by template and by bolt/step. | RPT-6 |
| P7.5 | **Worker performance** and **Device usage** reports. | RPT-7, RPT-8 |
| P7.6 | Excel export (ClosedXML) and PDF export (QuestPDF) for the session and traceability reports; exports match the on-screen filters. | RPT-9 |
| P7.7 | Admin dashboard: jobs by status, today's pass rate, online devices, waiting Black jobs, recent sessions; live updates for devices. | DASH |
| P7.8 | Verify immutability: no UI path or repository method updates/deletes results; tests assert it. | RPT-10 |

**Exit:** the UAT product's runs appear correctly in every report, with snapshot values, and export matches the screen.

---

## Phase 8: Hardening

Goal: production readiness.

| Task | What | Requirement |
|---|---|---|
| P8.1 | Calibration tracking: due dates, warnings on the Execute screen and device list, optional block when overdue (setting). | DEV-4 |
| P8.2 | Audit log completion: interceptor for Templates, Steps, Products, Components, Colour map, Devices, Settings, Users; Admin audit viewer with filters. | Feature 7 |
| P8.3 | Admin-approved **manual-entry fallback** (flagged, approver recorded, off by default; reported distinctly). | Feature 14 |
| P8.4 | Rework workflow with reason codes and Admin approval to restart a failed component; notifications. | Feature 5 |
| P8.5 | Security pass: headers, rate limits, upload checks, authorization audit of every endpoint, secret handling review. | Rules §5 |
| P8.6 | Performance pass: job board with 1,000+ jobs, report queries, index review. | NFR |
| P8.7 | Backup plan and restore test; deployment guide (IIS/Kestrel, WebSockets, migration bundle, offline asset check). | NFR |
| P8.8 | UAT with the real 6-panel locker; fix defects; sign-off. | PRD §11 |
| P8.9 | Optional if time allows: template import/export (JSON), QR/barcode job start, Supervisor role, multi-pass tightening. Each is a separate small task. | Features 4, 11–13 |

**Exit:** all Must stories pass; UAT signed off; deployment guide verified on a clean machine.

---

## Dependency Map

```
P0 (hardware + BM sample)  ─ runs alongside ─┐
P1 Foundation ─▶ P2 Templates ─▶ P3 Products ─▶ P4 BM & Jobs ─▶ P5 Worker (simulated) ─▶ P6 Devices ─▶ P7 Reports ─▶ P8 Hardening
                                                                        │                                   ▲
                                                                        └─ results data feeds ──────────────┘
```
Notes:
- P2.2 (evaluator) is needed before P5.6 and is built early on purpose.
- P6 depends on P0.1 to P0.3 for the firmware tasks (P6.3 to P6.5). P6.1 and P6.2 can start right after Phase 5.
- P7 can start in parallel with P6 once Phase 5 produces data.

---

## Suggested Milestones

| Milestone | Reached after | Demo |
|---|---|---|
| M1: Login and shell | Phase 1 | Admin and Worker log in to the themed app |
| M2: Design to product | Phase 3 | Build a 6-panel locker from templates |
| M3: Plan the work | Phase 4 | Upload BM, jobs appear Black-first |
| M4: Guided work (simulated) | Phase 5 | Worker completes a job with simulated readings |
| M5: Real hardware | Phase 6 | Real wrench and caliper feed the live screen |
| M6: Traceability | Phase 7 | Full report and export for a finished job |
| M7: Go-live | Phase 8 | UAT signed off |
