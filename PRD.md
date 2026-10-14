# PRD — Smart Torque Guidance & Traceability System

| Item | Value |
|---|---|
| Document | Product Requirements Document |
| Version | 1.0 |
| Status | Ready for build |
| Read with | `Architecture.md`, `Rules.md`, `Phases.md`, `Design.md`, `Memory.md` |
| One-line summary | A Wi-Fi enabled web app that guides workers through torque tightening and Vernier measurements on a component, evaluates every reading against the design, and stores a full traceability record. |

---

## 1. Problem Statement

A plant assembles products (for example a six-panel locker) where every component has bolts that must be tightened to a specific torque, and sides that must be measured to a specific dimension. Today these checks are manual, unguided and unrecorded. There is no proof of who tightened what, with which tool, to what value, and when.

The system must:

1. Show the worker the **component name**, **tightening position**, **required torque** and the **live applied torque**.
2. Store **every tightening and measurement result** for traceability.
3. Use **one application, one MVC architecture, one database** for **both instruments**: the USB digital torque wrench and the USB digital Vernier caliper.
4. Provide **two login roles**: **Admin** (configuration, planning, reporting) and **Worker** (execution).

Hardware chain: `Instrument (USB) → ESP32 gateway (Wi-Fi) → ASP.NET Core MVC app → MS SQL Server`.

---

## 2. Goals, Non-Goals, Success Metrics

### 2.1 Goals
- G1. One codebase handles torque (N·m) and dimensional (mm) checks, chosen **per step** from the template design, never by the worker.
- G2. Admin builds reusable **Templates** (one component) and composes them into **Products** (a collection of components).
- G3. Admin uploads a **BM (Buffer Management) Excel sheet**; rows become **Jobs** ranked by row colour, **Black first**.
- G4. Worker sees prioritised jobs, picks one, is guided step by step with a live reading, and submits when done.
- G5. Every result is logged against worker, device (transmitter ID), product, component, step, and time, with a snapshot of the target and tolerance used.
- G6. The system works on the plant LAN with no internet.

### 2.2 Non-Goals (out of scope for now)
- PLC integration. The design keeps a hook (`ExecutionMode` field) only.
- ERP integration.
- Native mobile apps (responsive web only).
- Multi-site / multi-plant (flagged in open questions).
- A Supervisor role (optional later).

### 2.3 Success Metrics
| Metric | Target |
|---|---|
| Live value latency (device → worker browser) on LAN | < 500 ms |
| Readings lost when Wi-Fi drops | 0 (device buffers and syncs) |
| Worker clicks to complete one step | 0 (reading auto-accepted when stable) |
| UAT | One real product (6-panel locker) completed end to end with a correct traceability report |
| Traceability | Any result is traceable to worker, device, job, step, time, and the exact target/tolerance in force |

---

## 3. Users and Roles

| Role | Who | Landing page | Can do |
|---|---|---|---|
| **Admin** | Planner / quality / engineer | Admin dashboard | Manage templates, products, BM/workload, reports, users, devices, settings |
| **Worker** | Shop-floor operator, often on a tablet next to the bench | Job board | See jobs, start one, execute steps, submit, view own history |

- One shared login page; redirect by role.
- Role-based authorization only: `[Authorize(Roles = "Admin")]`, `[Authorize(Roles = "Worker")]`.
- Optional later: **Supervisor** (approves reworks and manual entries without full admin rights).

---

## 4. Core Concepts

| Term | Meaning |
|---|---|
| **Instrument type** | `Torque` (N·m) or `Vernier` (mm). Selected per **step** in the template. |
| **Template** | Definition of one component (e.g. Left Panel): image, steps (bolts / sides), targets, tolerances. Versioned. |
| **Template step** | One bolt to tighten or one dimension to measure. |
| **Product** | A collection of template components (e.g. Locker 6-Panel). |
| **BM sheet** | Excel sheet whose row colour shows buffer status and so urgency. |
| **Job** | Unit of work for a product with priority and due date. |
| **Session** | One worker's execution of one component of a job on bound device(s). |
| **Transmitter ID** | Unique ID of the ESP32 / USB connector channel that sends readings. |
| **Traceability** | Showing who did what, with which device, when, and with what result. |

---

## 5. Functional Requirements

IDs are stable; use them in commits, tests and `Memory.md`.

### 5.1 Authentication and Authorization (AUTH)
| ID | Requirement |
|---|---|
| AUTH-1 | Cookie-based login via ASP.NET Core Identity; passwords hashed by Identity. |
| AUTH-2 | Two roles: `Admin`, `Worker`. Post-login redirect: Admin → `/Admin`, Worker → `/Worker`. |
| AUTH-3 | Admin can create, edit, deactivate users and reset passwords. Deactivated users cannot log in. |
| AUTH-4 | Anti-forgery tokens on every POST form. Account lockout after repeated failures. |
| AUTH-5 | A default Admin is seeded from configuration (user-secrets / env var), never from a password in source. |

### 5.2 Template Generation (TPL)
| ID | Requirement |
|---|---|
| TPL-1 | Admin creates a template with: name, unique code, optional description. |
| TPL-2 | Template **mode**: `Torque`, `Vernier`, `Mixed`. Mode only pre-fills the step instrument in the wizard; it can be changed per step. |
| TPL-3 | Admin enters the **number of steps** (torque: number of bolts; Vernier: number of sides/dimensions). Minimum 1. |
| TPL-4 | Admin uploads one **component image** (JPG/PNG, max 5 MB). Stored on disk, path stored in DB. |
| TPL-5 | For each step the Admin defines: **instrument** (Torque/Vernier), **label** (e.g. `Bolt 1 – Top Left`, `Side A – Length`), **target value**, **tolerance plus**, **tolerance minus**, **tolerance mode** (absolute or percent), **sequence number**, optional **marker X/Y** (click on image, stored as %), **max retries**. |
| TPL-6 | Unit is derived from the instrument: `N·m` for Torque, `mm` for Vernier. |
| TPL-7 | Validation: steps ≥ 1; target > 0; tolerance ≥ 0; code unique per version family; image type/size enforced; sequence numbers unique within a template. |
| TPL-8 | **Versioning:** editing a template that has ever been used by a session creates a new version. Old sessions keep pointing to the version they used. Unused drafts may be edited in place. |
| TPL-9 | Wizard of 3 stages: **Basic Info → Image & Steps → Values & Tolerances**, then preview and save. |
| TPL-10 | List with search and filter by mode, instrument and active/inactive. Detail/preview page. |
| TPL-11 | Optional: template import/export as JSON. |

### 5.3 Product Generation (PRD)
| ID | Requirement |
|---|---|
| PRD-1 | Admin creates a product: name, unique code, optional description and image. |
| PRD-2 | Admin adds components: choose a template (version), give it a **component name** (e.g. `Left Panel`) and **quantity** (default 1). |
| PRD-3 | Admin sets **assembly order** (drag to reorder). |
| PRD-4 | A product must have at least one component. The same template may be used more than once. |
| PRD-5 | Product stores a reference to the exact template **version** used. |
| PRD-6 | Clone a product to create a variant. |
| PRD-7 | List, builder, and detail page with expandable templates and steps. |

### 5.4 BM Upload and Workload (BM)
| ID | Requirement |
|---|---|
| BM-1 | Admin uploads an `.xlsx` BM sheet. |
| BM-2 | The parser reads, per row: product code, quantity, due date, remarks, and the **row/cell fill colour**. |
| BM-3 | Colour maps to priority (table below). The mapping is stored in a settings table and editable by Admin. |
| BM-4 | A **preview** screen shows parsed rows, detected colour, mapped priority and errors (unknown product code, missing quantity, unrecognised colour, bad date). Nothing is created until the Admin confirms. |
| BM-5 | On confirm, rows become Jobs. Rows with errors are skipped or fixed by the Admin in the preview. |
| BM-6 | Re-uploading an updated BM refreshes priorities of **open** jobs. Jobs already `InProgress` keep running. |
| BM-7 | **Automatic mode:** system creates jobs directly from the BM, ranks them, publishes to the worker board. Optional auto-assign by workload balance. |
| BM-8 | **Manual mode:** Admin assigns each job to a worker or leaves it open for any worker; Admin can override priority by hand. |
| BM-9 | Workload board: sortable and filterable by priority colour, due date, worker, status. |
| BM-10 | Black jobs are pinned to the top and highlighted. Urgent (Black) job creation raises a notification for workers. |

| Colour (configurable) | Buffer status | Priority | Order |
|---|---|---|---|
| **Black** | Stock-out / buffer fully consumed | **1 – Critical** | First |
| **Red** | Buffer almost consumed | 2 – Very High | Second |
| **Yellow** | Buffer partly consumed | 3 – Medium | Third |
| **Green** | Buffer healthy | 4 – Low | Fourth |
| **White / no fill** | Excess / no action | 5 – Lowest | Last, only if free |

**Ordering rule everywhere:** `Priority ASC (Black first) → DueDate ASC → UploadTime ASC`.

**Job lifecycle:** `Pending → Assigned → InProgress → Completed`, plus `OnHold`, `Rework`, `Cancelled`.

### 5.5 Worker Portal (WRK)
| ID | Requirement |
|---|---|
| WRK-1 | Job Board lists jobs in the ordering rule above, Black first, with colour-labelled priority badges. |
| WRK-2 | Worker selects a job and presses **Start**. The system creates a Session and **locks the job to that worker** (no two workers can take the same job). |
| WRK-3 | At Start, the worker selects (or is pre-mapped to) the device(s) needed: one torque device and/or one Vernier device, depending on the steps. |
| WRK-4 | **Execute screen** shows: component name; component image with the **current bolt/side highlighted**; position/measurement name; **required value** and tolerance band; **live applied value**; step counter (e.g. Bolt 3 of 8); instrument indicator. |
| WRK-5 | The screen switches **automatically** between torque gauge and Vernier readout depending on the step. The worker never selects the instrument. |
| WRK-6 | When a reading is accepted (stable value) it is evaluated, saved, and the next step is shown automatically. |
| WRK-7 | If a reading is out of tolerance: show FAIL, allow a retry up to `MaxRetries`, then move the job to `Rework`. |
| WRK-8 | After the last step, the worker presses **Submit**; the session closes with a final result; the job moves on (see 5.7). |
| WRK-9 | If the browser closes mid-session, the session stays `InProgress` and the worker resumes from the last saved step. |
| WRK-10 | Screens: Job Board, Execute (live), Summary, My History, Profile. |
| WRK-11 | Large, high-contrast, touch-friendly UI (workshop tablet). |

### 5.6 Reports and Traceability (RPT)
| ID | Requirement |
|---|---|
| RPT-1 | Every session logs: session ID, job, product, component/template (and version), worker, device (transmitter) ID(s), instrument type, start/end/duration, final result, rework count, remarks. |
| RPT-2 | Every step result logs: target, tolerance plus/minus, tolerance mode, **actual value**, PASS/FAIL, timestamp, attempt number, device ID, manual-entry flag. Target and tolerance are **snapshots** taken at the time. |
| RPT-3 | Session is `Pass` only if every required step has a passing final attempt. |
| RPT-4 | **Session report:** filter by date, worker, product, device, result. |
| RPT-5 | **Traceability report:** search by product serial, job number or device ID; shows full history. |
| RPT-6 | **Pass/Fail summary:** counts, first-pass yield, failure rate by template or bolt. |
| RPT-7 | **Worker performance:** jobs completed, average time, rework rate. |
| RPT-8 | **Device usage:** sessions per transmitter, last seen, calibration due. |
| RPT-9 | Export to **Excel** and **PDF**. |
| RPT-10 | Session results are **immutable**: no edit or delete through the UI. Corrections are new attempts or rework records with a reason code. |

### 5.7 Job Completion Rule
A Job has a `Quantity` (from the BM row). It is made of **one session per component per unit**, where each product component repeats `ProductComponent.Quantity` times. The job moves to `Completed` when every required session has `FinalResult = Pass`. See `Architecture.md` section "Job and Session Model" for the exact rule.

### 5.8 Devices (DEV)
| ID | Requirement |
|---|---|
| DEV-1 | Admin registers devices: transmitter ID (unique), name, instrument type, location, active flag. An API key is generated once, shown once, stored hashed. |
| DEV-2 | Device list shows online/offline (from heartbeat), last seen, calibration due. |
| DEV-3 | Readings from unregistered or inactive devices are rejected. |
| DEV-4 | Calibration tracking with due date and alerts (Phase 8). |

### 5.9 Admin Dashboard (DASH)
Jobs by status, today's pass rate, online devices, Black jobs waiting, recent sessions.

### 5.10 Additional Features (prioritised)
Items from the spec, marked by when to build them.

| # | Feature | Phase |
|---|---|---|
| 1 | Device management page | 6 |
| 2 | Calibration tracking and alerts | 8 |
| 3 | Sequence enforcement (steps must follow the defined order) | 5 |
| 4 | Multi-pass tightening (e.g. 50% → 100%) | Later |
| 5 | Rework/retry workflow with reason codes | 5 / 7 |
| 6 | Product serial numbers | 4 |
| 7 | Audit log of template/product/config changes | 8 |
| 8 | Admin dashboard | 7 |
| 9 | Notifications for Black jobs | 5 |
| 10 | Offline buffering on ESP32 | 6 / 8 |
| 11 | Supervisor role | Later |
| 12 | Template import/export (JSON) | Later |
| 13 | QR/barcode scan to start a job | Later |
| 14 | Admin-approved manual-entry fallback (flagged) | 8 |
| 15 | PLC-ready `ExecutionMode` field (Manual / PLC) | 1 (field only) |

---

## 6. Key User Journeys

### 6.1 End to end
```
Admin: create Templates → create Product (from templates) → upload BM → jobs created, Black first
Worker: login → pick job → Start (session opens, device bound)
Device: reading → ESP32 → Server → evaluate vs template → save result → push live update to browser
Worker: all steps pass → Submit → job Completed
Admin: Reports → traceability by device ID / job / serial
```

### 6.2 Failure handling
| Situation | Behaviour |
|---|---|
| Reading out of tolerance | Mark FAIL, show red + FAIL label, allow retry up to `MaxRetries` |
| Retries exhausted | Job → `Rework`; admin notified |
| Device offline | Warning banner; admin-approved manual entry (flagged) |
| Browser closes mid-session | Session stays `InProgress`; resume from last saved step |
| Reading arrives from the wrong instrument for the current step | Ignored for evaluation, worker sees a hint, logged |
| Duplicate reading (offline replay) | Ignored by device sequence number |

---

## 7. User Stories and Acceptance Criteria

### Admin
| ID | Story | Acceptance criteria |
|---|---|---|
| US-A1 | As an Admin I create a template for one panel with 8 torque bolts and 3 Vernier checks. | Template saves with 11 steps in the chosen sequence; each step has its own instrument, target, tolerance; image shown with markers; unit shows N·m or mm correctly. |
| US-A2 | As an Admin I edit a template already used in past work. | A new version is created; old sessions still show the old target/tolerance; the product keeps the old version until updated. |
| US-A3 | As an Admin I build a 6-panel locker from templates. | Product saves with 6 components in order; reuse of one template twice is allowed. |
| US-A4 | As an Admin I upload a BM sheet. | Preview shows each row with detected colour and priority; errors are flagged; confirm creates jobs; Black jobs are first on the board. |
| US-A5 | As an Admin I change the colour mapping. | New uploads use the new mapping; existing jobs are unchanged until re-uploaded. |
| US-A6 | As an Admin I assign a job manually or let the system publish it. | Manual: job shows `Assigned` to the chosen worker. Auto: job shows `Pending` on the shared board. |
| US-A7 | As an Admin I look up a product serial in the traceability report. | Full history appears with worker, device, step values, PASS/FAIL, times. |
| US-A8 | As an Admin I export sessions. | Excel and PDF contain the filtered rows and match the screen. |
| US-A9 | As an Admin I register a device. | The API key is shown once; the device can post readings; an inactive device is rejected. |

### Worker
| ID | Story | Acceptance criteria |
|---|---|---|
| US-W1 | As a Worker I see jobs with Black first. | Order is Priority → Due date → Upload time; badges carry text labels. |
| US-W2 | As a Worker I start a job. | A session opens; the job is locked; another worker cannot start it. |
| US-W3 | As a Worker I see the guided step. | Component name, image with highlighted bolt, label, required value and tolerance band, live value, step counter are visible. |
| US-W4 | As a Worker I tighten a bolt in tolerance. | Reading stable and within band → saved PASS → next step shown with no click. |
| US-W5 | As a Worker I tighten out of tolerance. | FAIL shown with icon and text; retry allowed; after max retries the job goes to Rework. |
| US-W6 | As a Worker I switch from a torque step to a Vernier step. | Screen changes to the Vernier readout automatically; the system listens to the caliper channel. |
| US-W7 | As a Worker I close the browser mid-job. | On return I resume at the next unsaved step. |
| US-W8 | As a Worker I submit. | Session closes with a final result; the job status updates. |

---

## 8. Non-Functional Requirements

| Area | Requirement |
|---|---|
| Performance | Live value latency < 500 ms on LAN. Job board loads < 1 s with 1,000 open jobs. |
| Reliability | Readings are never lost: ESP32 buffers offline, server writes in transactions, duplicates ignored. |
| Security | Role-based access, hashed passwords, HTTPS where possible, anti-forgery, server-side validation, per-device API keys (hashed), rate limiting on device endpoints. |
| Auditability | Immutable session results; audit log for configuration changes. |
| Usability | Worker completes a job with minimal interaction; large touch targets. |
| Deployment | IIS or Kestrel on a local server; SQL Server on the same LAN; works without internet (no CDN dependencies at runtime: vendor CSS/JS locally). |
| Backup | Daily SQL backup with retention policy. |
| Scalability | Multiple workstations and devices concurrently. |
| Accessibility | Colour is never the only signal; WCAG AA contrast; keyboard operable. |
| Time | Store UTC; display in the server's configured local time zone. |

---

## 9. Constraints and Assumptions

### Constraints
- Stack: C#, .NET 8 (or 9), ASP.NET Core MVC with Razor Views, EF Core, MS SQL Server, SignalR.
- UI theme: white `#FFFFFF` and purple/magenta `#BD1362`.
- Architecture: layered MVC with a service layer and repository/data layer.

### Assumptions (change in `Memory.md` if the owner decides otherwise)
- A1. One Job is created per BM row; the BM quantity is stored as `Job.Quantity` and each unit is run as its own set of sessions.
- A2. A workstation has at most one torque device and one Vernier device bound to a session.
- A3. Hardware is confirmed later; Phase 5 uses a simulated device so the whole app is testable first.
- A4. Single plant (no `SiteId`).
- A5. Units fixed: N·m and mm; any conversion happens at the gateway.

---

## 10. Risks and Open Questions

| # | Item | Action |
|---|---|---|
| 1 | USB protocol of the torque wrench and Vernier (HID keyboard / USB-serial CDC / vendor protocol) | Test with a PC first; capture the data format; build the firmware parser from it |
| 2 | Standard ESP32 is not a USB host | Use ESP32-S2/S3 (USB-OTG) or an external USB host shield; confirm before ordering hardware |
| 3 | Accept reading by "stable value" or button press | Decide per instrument; torque = peak capture, Vernier = hold/data button |
| 4 | Excel colour detection (theme colours vs RGB, especially Black) | Define BM sheet rules and a colour legend; nearest-colour match with tolerance; theme colour + tint handling |
| 5 | One device per worker or shared per station | Decide the binding rule; default is select at Start |
| 6 | Units (N·m vs lbf·ft, mm vs inch) | Fix units, convert at the gateway |
| 7 | Image storage | Disk with DB path |
| 8 | Single plant vs multi-site | Add `SiteId` only if needed |
| 9 | BM sheet exact column headers | Obtain a real sample BM sheet; make the header map configurable |
| 10 | What identifies "the same row" on BM re-upload | Default key `ProductCode + DueDate`; confirm with the owner |

---

## 11. Definition of Done (project level)

The project is done when:
1. All Must stories (US-A1…A9, US-W1…W8) pass their acceptance criteria.
2. One real product (6-panel locker) runs end to end with real or simulated devices and a correct traceability report.
3. Unit and integration tests pass in CI.
4. The app deploys on a plant server with SQL Server and runs offline.
5. `Memory.md` is up to date and the README explains setup, seeding, and the device simulator.
