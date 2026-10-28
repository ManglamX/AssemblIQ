# Memory � Session State and Progress Log

**This file is the project's working memory. It is the only file that is meant to change every session.** Read it first, update it last.

| Item | Value |
|---|---|
| Project | Smart Torque Guidance & Traceability System |
| Source spec | `Smart_Torque_Guidance_Traceability_System.md` (v1 spec, kept for reference) |
| Memory created | 2026-10-12 |
| Last updated | 2026-10-27 |
| Current phase | **Phase 7 Complete — Reports & Dashboard** (verified against code), plus owner-driven features landed outside the phase IDs: per-unit tracking tokens, central API-key directory, operator defect workflow, Assembly Management & Workload Assignment |
| Current task | Ready for Phase 8 (Hardening): start at P8.1 calibration tracking. Note the 4 feature blocks above are undocumented in Phases.md and need owner decisions where flagged (D-045 supersedes WRK-7). Delivered 2026-10-27 on owner requests: Product Builder duplicate-code guard at Step 1 + step-scoped alerts (D-051), product image crop-then-upload + zoomable viewer (D-052), multi-image product gallery (D-053), template generator marker drag/zoom-slider + placement fix (D-054), per-marker size control persisted template→worker (D-055), large-view marker popup (D-056), eye-button image preview instead of inline thumbnail (D-057), popup per-marker size selection fix (D-058), Step-3 product summary image large-view popup (D-059), compulsory marker assignment before Generate/Step-3 (D-060) |
| Build status | Clean build, 0 errors, 0 warnings (`dotnet build -warnaserror`) |
| Test status | 341/341 passing (build 0 warnings / 0 errors, `dotnet format --verify-no-changes` exit 0) |

---

## How to use this file (for any coding agent)

1. **Start of session:** read this whole file, then `Rules.md`, then the docs you need (`PRD.md`, `Architecture.md`, `Phases.md`, and `Design.md` before UI work).
2. Pick the **next unchecked task** in the tracker below (in order). Do not skip ahead.
3. **During the session:** if you make a decision the docs do not cover, add it to *Decisions* (next free `D-nnn`). If you are blocked or unsure, add to *Open Questions* and ask the owner.
4. **End of session:** tick finished tasks, update *Current phase/task* and *Last updated*, add a *Session Log* entry, record *Known Issues* and *Next steps*.
5. Never delete history from this file; append. Mark old decisions `Superseded by D-nnn` if changed.

---

## Progress Tracker

Legend: `[ ]` not started � `[~]` in progress � `[x]` done and passing the gate in `Rules.md` �9 � `[!]` blocked (see Open Questions)

### Phase 0: Preparation
- [ ] P0.1 USB protocol of the torque wrench captured and documented
- [ ] P0.2 USB protocol of the Vernier caliper captured and documented
- [ ] P0.3 ESP32 model with USB host decided
- [ ] P0.4 Real BM Excel sample and colour legend obtained
- [ ] P0.5 Open questions answered by the owner (device binding, BM re-upload key, accept rule, units)

### Phase 1: Foundation
- [x] P1.1 Solution and projects, dependency rule, editorconfig, gitignore
- [x] P1.2 DbContext, Identity, SQL Server connection, first migration
- [x] P1.3 Seed roles and first Admin (from config)
- [x] P1.4 Login/logout, lockout, anti-forgery, role redirect, fallback policy
- [x] P1.5 Theme (`theme.css`), layouts, shared partials, vendored Bootstrap
- [x] P1.6 Admin dashboard shell and Worker job board shell
- [x] P1.7 User management
- [x] P1.8 Serilog, error handling, `/health`, TimeProvider, settings table
- [x] P1.9 Test project, role authorization tests

### Phase 2: Templates
- [x] P2.1 Template and TemplateStep entities, enums, migration
- [x] P2.2 Tolerance evaluator with unit tests (17 tests passing)
- [x] P2.3 Image upload service (JPG/PNG, <= 5 MB, magic byte check, GUID naming)
- [x] P2.4 Wizard stage 1 (basic info, unique code, mode selection cards)
- [x] P2.5 Wizard stage 2 (image AJAX upload, step count, preview)
- [x] P2.6 Wizard stage 3 (per-step instrument, target, tolerances, click-to-place markers)
- [x] P2.7 Preview, save, detail page (with calculated tolerance bounds)
- [x] P2.8 Template list, search, filters (mode, instrument, status), activate/deactivate
- [x] P2.9 Versioning (used templates create next version, unused edit in place; all versions viewable)
- [x] P2.10 AuditLog table and template audit tracking on create, update, version, activate

### Phase 3: Products
- [x] P3.1 Product and ProductComponent entities, migration
- [x] P3.2 Product create/edit
- [x] P3.3 Product builder with ordering
- [x] P3.4 Product detail with expandable steps
- [x] P3.5 Clone product
- [x] P3.6 Product list
- [x] P3.7 Update component to latest template version

### Phase 4: BM and Jobs
- [x] P4.1 BM, Colour map, Job, Notification entities and seed
- [x] P4.2 Colour map admin screen
- [x] P4.3 BM parser (headers, rows, parsing) with fixtures
- [x] P4.4 Colour detection and matching with tests
- [x] P4.5 Upload and preview screens
- [x] P4.6 Confirm import, job creation, Black notification
- [x] P4.7 PriorityService ordering with tests
- [x] P4.8 Workload board (Black pinned, 4px left border)
- [x] P4.9 Manual and automatic assignment
- [x] P4.10 Re-upload refresh rule
- [x] P4.11 Job detail, hold/resume/cancel, state machine

### Phase 5: Worker Portal (simulated readings)
- [x] P5.1 Device, Session, SessionResult entities and indexes
- [x] P5.2 Minimal device registration and API key
- [x] P5.3 Worker job board
- [x] P5.4 Start job with optimistic locking and device selection
- [x] P5.5 Component picker and resume
- [x] P5.6 SessionService and ReadingProcessor with tests
- [x] P5.7 LiveReadingHub and events
- [x] P5.8 Execute screen (torque gauge and Vernier readout, auto switch)
- [x] P5.9 Retry and fail flow
- [x] P5.10 Submit and Summary
- [x] P5.11 Sequence enforcement
- [x] P5.12 Dev simulator page and console tool
- [x] P5.13 My History and Profile
- [x] P5.14 Black-job notification to workers

### Phase 6: Device Integration
- [x] P6.1 Device API endpoints, auth, rate limits, integration tests
- [x] P6.2 Device management UI and online status
- [x] P6.3 Stable-value rules from real protocol
- [x] P6.4 ESP32 firmware (USB host, Wi-Fi, posting)
- [x] P6.5 Firmware offline buffer, replay, NTP
- [x] P6.6 LAN latency test (< 500 ms)
- [x] P6.7 Real device end-to-end worker test

### Phase 7: Reports and Dashboard
- [x] P7.1 ReportService and filters
- [x] P7.2 Session report and detail
- [x] P7.3 Traceability report
- [x] P7.4 Pass/Fail summary
- [x] P7.5 Worker performance and Device usage
- [x] P7.6 Excel and PDF export
- [x] P7.7 Admin dashboard
- [x] P7.8 Immutability verification

### Phase 8: Hardening
- [ ] P8.1 Calibration tracking
- [ ] P8.2 Audit log completion and viewer
- [ ] P8.3 Manual-entry fallback with approval
- [ ] P8.4 Rework workflow with reason codes
- [ ] P8.5 Security pass
- [ ] P8.6 Performance pass
- [ ] P8.7 Backup, restore and deployment guide
- [ ] P8.8 UAT with the 6-panel locker and sign-off
- [ ] P8.9 Optional extras (JSON import/export, QR start, Supervisor, multi-pass)

---

## Decisions

Status: **Spec** = stated in the original spec � **Resolved** = a gap in the spec filled while writing these docs (change only with owner approval) � **Provisional** = best guess, needs owner confirmation.

| ID | Date | Decision | Status | Where it applies |
|---|---|---|---|---|
| D-001 | 2026-10-12 | Stack: C#, .NET 8 (or 9), ASP.NET Core MVC with Razor, EF Core, MS SQL Server, SignalR, Identity, Bootstrap 5 | Spec | All |
| D-002 | 2026-10-12 | One app, one DB, one codebase for both instruments; instrument chosen **per template step**; worker never selects it | Spec | Architecture �2 |
| D-003 | 2026-10-12 | Theme is white `#FFFFFF` + `#BD1362`; PASS/FAIL colours are functional and separate; use icons + labels, priority shown as labelled badges | Spec | Design |
| D-004 | 2026-10-12 | Priority colours: Black 1, Red 2, Yellow 3, Green 4, White/no fill 5; ordering `Priority ? DueDate ? Upload time`; mapping configurable | Spec | PRD �5.4 |
| D-005 | 2026-10-12 | Roles: Admin, Worker only; Supervisor deferred | Spec | PRD �3 |
| D-006 | 2026-10-12 | Recommended comms: WebSocket (SignalR) for live values + HTTP POST for confirmed readings | Spec | Architecture �8�9 |
| D-007 | 2026-10-12 | `TemplateCode` is unique **together with `Version`** (not alone), with one `IsLatest` per code, because the spec asks for both unique codes and versioning | Resolved | Architecture �5.3 |
| D-008 | 2026-10-12 | `Sessions` carry `TorqueDeviceId` and `VernierDeviceId` (not a single `DeviceId`); spec �8.2 is followed over the single-device sentence in �9.4 | Resolved | Architecture �5.3 |
| D-009 | 2026-10-12 | A **Job** has `Quantity`; a **Session** is per `(UnitNo, ProductComponent, ComponentInstanceNo)`; the job completes when all required sessions pass. Added `Job.Quantity`, `Job.QuantityCompleted`, `Session.UnitNo`, `Session.ComponentInstanceNo` | Resolved | Architecture �6 |
| D-010 | 2026-10-12 | `Cancelled` added as Job status 7 (the spec lists it in the lifecycle but not in the status column) | Resolved | Architecture �5.3 |
| D-011 | 2026-10-12 | Readings carry an optional device sequence number `seq`; unique `(DeviceId, DeviceSeq)` makes offline replay idempotent | Resolved | Architecture �8 |
| D-012 | 2026-10-12 | Evaluator: inclusive bounds, `decimal`, 3-decimal rounding away from zero; percent mode = target � (1 � tol/100) | Resolved | Architecture �7.1 |
| D-013 | 2026-10-12 | Live (non-stable) readings are broadcast only, never stored; only `isStable = true` readings are saved | Resolved | Architecture �7.5 |
| D-014 | 2026-10-12 | Template editing: unused ? edit in place; used ? new version; products keep old version until explicitly updated | Resolved | Architecture �11 |
| D-015 | 2026-10-12 | BM colour detection: read the ProductCode cell fill (fallback: other cells in the row; none = White); resolve RGB, theme+tint, indexed; nearest match within a configurable RGB distance (default 40); unmatched = Error needing Admin action | Resolved | Architecture �7.3 |
| D-016 | 2026-10-12 | Front-end libraries are vendored; no CDN, no web fonts downloaded at runtime (plant is offline) | Resolved | Rules �2, Design �3 |
| D-017 | 2026-10-12 | Manual entry fallback is off by default, requires Admin approval, flagged in reports | Resolved | PRD feature 14 |
| D-018 | 2026-10-12 | Priority badges have their own tokens (`--pri-*`) and always show a text label and a shape | Resolved | Design �2, �5.5 |
| D-019 | 2026-10-12 | Session results are append-only; reports read the snapshot values, never the live template | Spec | Architecture �5.5 |
| D-020 | 2026-10-12 | Build and test the whole worker flow against a **simulated device** before hardware is confirmed | Spec | Phases P5.12 |
| D-021 | 2026-10-12 | **BM re-upload row key = `(ProductCode, DueDate)`**; unmatched old rows are listed, not auto-cancelled | Provisional | Architecture �7.3 |
| D-022 | 2026-10-12 | **Device binding:** worker selects the torque and/or Vernier device at Start (default to last used); a station may be pre-mapped later | Provisional | PRD �9 A2 |
| D-023 | 2026-10-12 | **Accept rule:** torque = peak/stable from the wrench; Vernier = hold/data button or stable for `StableHoldMs`; finalised after P0 | Provisional | Architecture �7.7 |
| D-024 | 2026-10-12 | **BM columns:** `ProductCode`, `Quantity`, `DueDate`, `Remarks`, header in row 1, mapped via configurable `BmHeaderMap`; confirm against the real sheet | Provisional | Architecture �7.3 |
| D-025 | 2026-10-12 | **Units** fixed to N�m and mm; firmware converts | Provisional | PRD �9 A5 |
| D-026 | 2026-10-12 | **Image storage:** disk under `uploads/` with a DB path | Spec | PRD �10 |
| D-027 | 2026-10-12 | Single plant, no `SiteId` (add later if needed) | Provisional | PRD �9 A4 |
| D-028 | 2026-10-13 | Product create/edit (P3.2) and the component builder (P3.3) are one screen (Design �7.5), saved atomically; a product must have **=1 component** and components may reuse the same template. Assembly order is taken from the submitted row order on every save | Resolved | Architecture �5.3, Design �7.5 |
| D-029 | 2026-10-13 | `(ProductId, AssemblyOrder)` is a **non-unique** index (the spec does not require uniqueness), so component rows can be replaced in one `SaveChanges` without a transient unique-key clash | Resolved | Architecture �5.3 |
| D-030 | 2026-10-13 | `ImageUploadService` gained a folder-aware overload (`uploads/{subfolder}`) so products use `uploads/products/`; the original template call site is unchanged | Resolved | PRD �10, Architecture �5.3 |
| D-031 | 2026-10-13 | P4.1 schema: `DueDate` maps to `DateOnly?` (`date`); `Job.SourceBmRowId` is a nullable FK with `SetNull` so the re-upload rule (BM-6) can find a job's originating row; `Notification.TargetRole` targets a role (not a user) so every worker sees Black-job alerts; colour map gets `IsActive` soft delete + audit columns per �5.2 | Resolved | Architecture �5.3, �7.3 |
| D-032 | 2026-10-13 | Colour map admin lives at `/ColourMap` (`ColourMapController`). Design �6.1 fixes the sidebar to 8 items, so it gets **no** nav entry; it is linked from the BM upload screen (P4.5) instead. Deactivate is a soft delete and the last active shade cannot be deactivated, because the importer needs at least one | Resolved | Design �6.1, PRD BM-3 |
| D-033 | 2026-10-13 | A colour swatch shows a **stored** hex via `background-color:@value`. Design �7.6 requires a swatch, so this is data, not a design colour � views still contain no hex *literals*; design tokens remain only in `theme.css` | Resolved | Rules �7.1, Design �7.6 |
| D-034 | 2026-10-13 | `ClosedXML` is referenced by `SmartTorque.Services` (it already was by `Web`), because Architecture �3.2 names it for Excel **import** and the parser belongs in Services, not the controller. P4.3 reads only *solid RGB* fills and returns `null` for theme/indexed/automatic fills � resolving those is P4.4's colour detector. Test fixtures are real committed `.xlsx` files under `tests/SmartTorque.Tests/Fixtures/`, copied to the output dir, per P0.4 | Resolved | Architecture �3.2, �7.3, Rules �3 |
| D-035 | 2026-10-13 | Due dates are accepted as a real date, an Excel serial number or text (BM-2). Text is tried with `CurrentCulture` then `InvariantCulture`, so the **fixtures use ISO text dates** (`2026-11-05`) to keep the tests culture-independent � do not put ambiguous `dd/MM/yyyy` strings in fixtures | Resolved | PRD BM-2 |
| D-036 | 2026-10-13 | Colour detection resolves solid RGB, theme + tint, indexed, and no fill � and **no fill now yields `#FFFFFF` rather than `null`**, so `ParsedBmRow` carries a `Priority` and every row has a hex for the preview legend. Tint uses the ECMA-376 two-branch formula (0 = unchanged, 0.5 = branch boundary, 1 = black); it is discontinuous exactly at 0.5, which is accepted pending a real supplier file (Q-03). An unrecognised colour is a **Warning**, not an Error, and defaults to White | Resolved | PRD BM-4, BM-5, Architecture �7.3 |
| D-037 | 2026-10-13 | The BM section routes are `/Bm` (import hub: upload + recent uploads) and `/Bm/Preview/{id}`; the workload board takes `/Bm/Workload` in P4.8, so neither screen has to be re-pointed later. `/ColourMap` keeps no sidebar entry because Design �6.1 fixes the nav at eight items, so an **"Edit colour map" link sits in the legend panel on both BM screens** � D-032's promised entry point. | Resolved | Design �7.6, �6.1; D-032 |
| D-038 | 2026-10-13 | Preview persistence: the workbook is written to `uploads/bm/{guid}.xlsx` **before** parsing, so a rejected file never leaves a `BmUpload` row pointing at nothing, and the parse runs from the stored copy. A `Failed` upload keeps no error text, so file-level errors are **re-read from the stored workbook** when the preview opens instead of adding a column. **Skip is allowed only on importable rows** (Ok/Warning) � an Error row can never be imported, so skipping it is meaningless � and restoring recomputes the single condition that can downgrade Ok to Warning (colour within tolerance), which is why `BmRow` needs no "original status" column. | Resolved | PRD BM-7, BM-8; Architecture �7.3; Design �7.6 |
| D-039 | 2026-10-13 | Destructive confirmations on the BM screens use a native `window.confirm` that names the object ("Remove the upload 'file.xlsx' and its 42 rows?"). Design �5.8's shared modal component is **deferred** to a UI pass rather than built inside this task. | Deferred | Design �5.8, �7.6 |

| D-040 | 2026-10-14 | The owner asked for Entity Framework generated models in `src/SmartTorque.Web/Models` — produced by `dotnet ef dbcontext scaffold` (17 entity POCOs + a `SmartTorqueDb` context, `--no-onconfiguring`, `= null!` on DbSets). They are **reference only** (schema inspection / comparison); the app keeps using `SmartTorque.Data.AppDbContext` + `SmartTorque.Core.Entities`, and `SmartTorqueDb` is never registered in DI. Because two contexts now exist, every `dotnet ef` command needs `--context AppDbContext`. | Resolved | `SmartTorque.Web/Models`, `dotnet ef` tooling |
| D-041 | 2026-10-15 | BM extraction enhancement: (1) Added Blue (`#0070C0`, Priority 5) to `PriorityLevel` enum and updated White to Priority 6 to match real plant BM colour coding (Black 1, Red 2, Yellow 3, Green 4, Blue 5, White 6); updated DB seeds and colour maps. (2) `BmImportService` auto-detects `Item Code` as the common item code column across BMs. (3) Added column inspection (`InspectHeadersAsync` / `/Bm/InspectHeaders`) and prompt/modal asking the user for the **Description** column before parsing/rendering the BM preview table. (4) `BmColourDetector` scans row fill, first cell, and used cells across the row if the code cell is unstyled. | Resolved | PRD BM-1, BM-4, Architecture §7.3 |
| D-042 | 2026-10-15 | Deterministic auto-generated template codes: (1) Removed user liberty to enter arbitrary template codes; user provides Name, Description, Mode, and specifications. (2) `TemplateCodeGenerator` derives deterministic codes: for Torque Wrench based on bolt count, target torque (N·m), and tolerance (e.g. `TW-4B-25NM-T2`); for Vernier Caliper based on side count, target dimensions (mm), and tolerance in mm (e.g. `VC-2S-100MM-T0.5`); mixed mode combines both. (3) If a template with that generated specification already exists in the library, the system displays the existing template's full details and direct action buttons to use it in Product Builder (`/Product/Create?templateCode=...`) or view its details. | Resolved | Template Management, Architecture §5.3, §11 |

| D-043 | 2026-10-16 | **Per-unit tracking tokens:** a Job's `Quantity` units materialise as `JobUnit` rows (40-char `PRD-`+36-hex `TokenNo`, unique index; unique `(JobId, UnitIndex)`), created lazily by `SessionService.EnsureJobUnitsExistAsync` and at BM confirm; tokens identify physical units end-to-end (session, summary, reports). Migration `AddJobUnitsAndApiKeyDirectory` | Resolved | Architecture §5.3/§6 (addendum) — commit dff2e6d |
| D-044 | 2026-10-16 | **Central API-key directory:** `ApiKeyDirectoryEntry` + `ApiKeyAccessGrant` hold the one-time-visible device registration keys; non-device seed keys are purged at startup (kept hardware-keys-only, commit 53d4361); Admins request access and the Main Admin approves/revokes | Resolved | Device mgmt / DEV-1 — commits dff2e6d, 53d4361 |
| D-045 | 2026-10-17 | **Operator defect workflow (supersedes WRK-7 retry exhaustion):** retries are now unlimited; the old "after MaxRetries the session fails and the job goes to Rework" rule no longer applies. Instead the worker may **Record Defect & Proceed** (POST `ForceFail=true`, `ReasonCode` default `OPERATOR_FORCE_FAIL`): a FAIL attempt is logged (append-only snapshot intact) and the step advances. A unit ends Failed → Job `Rework` only when steps of that session end non-passed (e.g. an operator-recorded defect). `MaxRetries` remains a per-step hint in the template/UI. Owner-requested change — confirm at next opportunity | Resolved (pending Q-11 confirm) | PRD §5.5 WRK-7, Architecture §7.5 — commit fc10af7 |
| D-046 | 2026-10-17 | **Unit-level execution UX:** `ExecutionStateDto` now carries `UnitTokenNo` and the full roster (`ExecutionUnitDto`, `IsCurrent`); `StartOrResumeSessionAsync` takes an optional `unitNo`; `SwitchUnitAsync` abandons the active session and restarts on the chosen unit; Execute screen shows a unit switcher (token + status); sessions list/detail and Session Detail's red **Defect Alert** panel surface per-step failed last attempts with deviations | Resolved | Worker portal, RPT-1/RPT-2 — commit fc10af7 |
| D-047 | 2026-10-19 | **Assembly Management & Workload Assignment:** `AssemblyLine` (unique `AssemblyCode`), `ProductAssemblyMapping` (`IsDefault`, unique per product+line), `ApplicationUser.CurrentAssemblyLineId`, `Job.AssignedAt`/`AssemblyLineId`/`EstimatedWorkload decimal(18,2) default 1.0`. **Strict line boundary:** a job may only be assigned to workers whose current line matches (manual, import, auto). Auto-assign is least-loaded (workload → task count → last-assigned) rebalanced in-batch; no hard capacity cap exists. Unmapped products fall back to a general pool of line-less workers; a line with no active workers keeps its jobs `Pending` and raises an `AssemblyAlert` notification (Type 3, TargetRole Admin). BM confirm stamps each job with the product's default line. Deleting a line with jobs soft-deactivates it | Resolved | PRD addition (unmapped to IDs), Architecture addendum — commit 9ad0380 |
| D-048 | 2026-10-19 | **Admin sidebar is now 9 items** (Assembly between Products and BM & Workload; `_AdminLayout`). Design §6.1's "sidebar fixed at 8 items" is superseded; ColourMap still has no nav entry — D-032 stands | Resolved | Design §6.1 (supersede), commit 9ad0380 |
| D-049 | 2026-10-19 | **Product builder: component name optional.** Blank names auto-fill from the chosen template's Name (client: `data-name` in `product-builder.js`; server: `ProductController.NormalizeComponents` fallback chain: template option name → row TemplateName → `Component {TemplateId}`) | Resolved | PRD-2, Design §7.5 — commit 9ad0380 |
| D-050 | 2026-10-19 | **Settings page (`/Settings`, 10th admin nav item — owner-requested out-of-plan work):** the orphaned `ISettingsService` is now implemented (`SettingsService`, DI-registered; `GetOverviewAsync`, typed getters, validated `SaveApplicationSettingsAsync` with `ApplicationSettingsSaveRequest` — upserts only changed keys and writes **one AuditLog row per save** with an old→new JSON). Page is honest about each key: **Enforced** = `DeviceOfflineSeconds` (Device list online status), `MaxRetriesDefault` (wizard Stage-3 empty-row prefill; per-step `MaxRetries` stays an editable hint, D-045), `ImageMaxMb` (new optional `maxBytes` param on `IImageUploadService.SaveAsync`, read per call by Template+Product upload actions), `BmHeaderMap` (JSON editor: valid object, no explicitly blank values — absent keys fall back to importer defaults, blank input deletes the row). **Read-only** = `StableHoldMs` (device-side, firmware `config.h` — the server never invents stability), `BmColourMatchTolerance` (reserved — each shade's own `MatchTolerance` governs; page links to `/ColourMap`), `SessionIdleTimeoutMin` + `RequireManualEntryApproval` (planned Phase 8, not changeable through the request shape). The sidebar link pre-dates this work; D-048's "9 nav items" is superseded by 10. | Resolved | Architecture §5.3, §15; Design §6.1 (superseded again); commit pending |
| D-051 | 2026-10-27 | **Product Builder duplicate-code guard moved to Step 1 (owner-reported bug):** leaving Step 1 toward Step 2/3 first runs AJAX `POST /Product/CheckCode` (truth via `ProductService.IsCodeUniqueAsync(code, excludeProductId)`) and blocks the transition with an inline `#product-code-feedback` message ("This code is already used by another product.") + `.is-invalid` on `#ProductCode`; a debounced-input/blur listener gives live availability feedback while typing; the final POST runs the same guard client-side (`formSubmitGuard`) and `ProductController.Create/Edit` keeps its server-side re-check as the backstop. Edit mode excludes itself via the hidden `EditingProductId` field; token-based race guard cancels stale in-flight checks. **Banner alerts are step-scoped:** `showAlert(msg, isError, ownerStep)` + `hideAlert()` so an alert raised for Step 1 disappears when another step is entered (second owner-reported bug), and the stale Step-1 banner is cleared once its conditions no longer apply; untagged banners keep legacy dismiss-only behaviour. | Resolved | Product Builder (D-028 screen) — `ProductController.CheckCode`, `product-builder.js`, `Builder.cshtml` |
| D-052 | 2026-10-27 | **Product image picker: crop-then-upload + eye-button large view (owner-requested UX):** picking a file in the Product Builder (Step 1) opens the shared `#cropModal` editor in "product" mode (`cropTarget` switch in `openCropModal(src, target)`; modal title subject becomes "Product"); **"Crop & Use Image"** uploads the cropped JPEG (canvas → blob) and **"Use Without Cropping"** uploads the original file, both to `POST /Product/UploadImage` (form token appended; the stale inline "Upload" button and handler were removed). After upload only a 72px thumbnail shows with a **View (eye)** button opening the new `#image-view-modal` large view, plus the existing Remove/Clear button (also resets the file input). The Step-2 component-image crop/marker flow is unchanged (default "gen" target). | Resolved | Product Builder (D-028 screen) — `product-builder.js`, `Builder.cshtml` |
| D-053 | 2026-10-27 | **Product images are a multi-image gallery (owner request) while the single `Product.ImagePath` is kept as the type-0 primary so lists/detail/etc. keep working:** new `ProductImage` entity (ProductId, ImagePath, SortOrder 0-based, CreatedAt; FK cascade, index on (ProductId, SortOrder)), migration `AddProductImages` **backfills the gallery from existing `Products.ImagePath` rows**. `SaveProductRequest` gained `IReadOnlyList<string>? Images`; the service derives `ImagePath = gallery.FirstOrDefault()` and replaces gallery rows on every create/update (dedupe case-insensitive + trim + **only paths starting `uploads/products/` survive** — path traversal blocked; empty gallery falls back to the legacy ImagePath so old callers/clone stay single-image; `CloneAsync` copies the gallery in order). Builder VM posts non-sequential lists via the `Images.Index` sentinel pattern (`Images.Index` + `Images[k]` hidden inputs regenerated per chip by JS). Builder UI: file input is `multiple` — picking N files queues them one at a time through the shared crop editor (per-file consume tracked by `productCropConsumed`; closing the modal without using a file **abandons the rest of the batch**; the queue advances only after both modal-close and upload settle — `productHideSettled`/`productUploadSettled`); each image is a chip (64px thumb + eye → zoomable `#image-view-modal` viewer + trash → remove) with hidden inputs renumbered (`productImageSeq`); the first gallery image fills `#image-path` and the Step-3 summary preview. Edit mode seeds the JS state from server-rendered hidden inputs. Detail page still shows only the primary image (first) — follow-up if the owner wants an on-page gallery. Note: `.editorconfig` `insert_final_newline = false` — dotnet format's FINALNEWLINE rule REMOVES trailing newlines (don't add them). | Resolved | Product Builder + DB schema (migration 20261027061041) — `ProductImage.cs`, `AppDbContext.cs`, `ProductService.cs`, `ProductViewModels.cs`, `ProductController.cs`, `product-builder.js`, `Builder.cshtml` |
| D-054 | 2026-10-27 | **Template Generator (Builder Step 2) marker placement rebuilt (owner-reported bug + 2 requests):** *Root cause of "marker spawns somewhere else":* click placement measured the cursor relative to `#gen-component-image`, but pins were positioned with % of `#gen-image-container` — a full-width dark box that letterboxes the `mx-auto`-centered image — so every pin drifted with the letterbox (same bug existed in the similarity modal's pin layers, whose plain static layer inside `position-relative` wraps resolved % against the padded wrap box). Both now use a dedicated overlay **sized exactly to the img rect** (`syncGenMarkerLayer` / in-modal geometry sync; re-synced on img `load` + window `resize`). Click-to-place keeps its bounds-guarded img-relative math and clamps 0–100. *Drag & drop:* pins are pointer-event draggables (`pointerdown` selects with `selectGenStep(idx, true)` — a **skip-render** variant added because a full `renderGenMarkers()` during pointerdown destroys the grabbed dot and kills its pointer capture; `pointerup` → full render refresh), move updates the row's hidden `MarkerX/Y` inputs live, clamped to the image area, `touch-action:none`, cursor grab/grabbing; tapping a pin stops propagation so the same tap can't re-place the same row and auto-advance. *Size controls:* S/M/L presets (16/24/32) now share the bar with a **range slider `#gen-marker-size-slider`** (14–42 px, live px readout `#gen-marker-size-value`, persisted in `localStorage` as before); presets snap the slider, the slider de-clamps to presets quietly. | Resolved | Product Builder (D-028 screen Step 2) — `product-builder.js`, `Builder.cshtml` |
| D-055 | 2026-10-27 | **Per-marker size persists template→worker (owner request):** `TemplateStep` gained `MarkerSize int?` (migration `AddTemplateStepMarkerSize`, AppDbContext; service-side clamp 14–56 in `MapSteps`). The generator slider/S/M/L now resize **individually the selected row's placed marker** (sizes stamped at placement time into per-row hidden `.step-marker-size` inputs; cleared when the marker is cleared) — with no marker selected they keep setting the default for future placements; `selectGenStep` syncs controls to the selection. Persisted through `QuickStepDto` / `StepRequest` / `StepRowViewModel`(+Stage3 hidden round-trip) / `StepDetailViewModel`, similarity projections (`markerSize` on both sides → modal dots use each step's own size, fallback 18), worker `ExecutionStepDto` (render at per-step `data-base-size`, defaults 28/32 in the blueprint/modal) and the device SSE payload. Worker Execute + template Detail size tools became **relative scale tools** (each pin's base × localStorage factor; 28 px/24 px = 100 %; labels show percent). Default authoring sizes stay shared with the wizard, whose Stage3 editor got the same per-row semantics (stamped on `placeMarker`, sizes read by `addMarkerDot`, slider re-sizes only the selected placed marker). | Resolved | Product Builder Step 2 + Wizard Stage3 + Template Detail + Worker Execute + Device API — `TemplateStep.cs`, `AppDbContext.cs`, migration 20261027104024, `TemplateService.cs`, `SessionService.cs`, `TemplateController.cs`, `ProductController.cs`, `ProductViewModels.cs`, `TemplateViewModels.cs`, `ProductViewModels.QuickStepDto`, `DeviceApiController.cs`, `product-builder.js`, `Builder.cshtml`, `WizardStage3.cshtml`, `Detail.cshtml`, `Execute.cshtml` |
| D-056 | 2026-10-27 | **Large-scale marker placement popup (owner request — inline image was too small to mark):** new `#marker-large-modal` (modal up to `min(96vw,1250px)` × ~78vh dark canvas) reachable three ways: the row's pin button, the new inline "Large View" button on the placement canvas, and dropdown presets; inline image also grew 280→340 px. The modal reuses the builder engine: img-rect-mirrored overlay (`syncMmMarkerLayer`), same per-marker sizes (`getRowMarkerSize`), draggable dots with a modal-local skip-render selection (`selectMmInplace` keeps the grabbed dot alive; `updateRowMarker` still writes the row's hidden inputs), click-to-place with letterbox guard + auto-advance, own slider/S-M-L that share `setMarkerSize`/`syncMarkerSizeControls` (both toolbars stay in sync). Selection changes inside the modal propagate to the main table (`selectGenStep` refreshes the modal badge too). `hidden.bs.modal` re-renders the inline layer. No saved-image marker: without an uploaded image the inline `#gen-upload-error` notice explains and no modal opens. | Resolved | Product Builder Step 2 — `product-builder.js`, `Builder.cshtml` |
| D-057 | 2026-10-27 | **Component-image preview is an eye button, not a small inline image (owner request):** the Step-2 upload card no longer renders the picture; after upload it shows a compact row ("Component image ready for marker mapping") whose **View (eye)** button opens the shared zoomable `#image-view-modal` (title now generic "Image Preview"; zoom/pan/double-click). State swaps `#gen-image-preview` for `#gen-image-view-row` (shown on upload, hidden on remove); viewer URL prefers the live `#gen-component-image` src (reflects crop re-uploads) falling back to `uploads/...` from `#gen-image-path`. The marker canvas below is unaffected (still the placement surface, incl. its Large View button). | Resolved | Product Builder Step 2 upload card — `product-builder.js`, `Builder.cshtml` |
| D-058 | 2026-10-27 | **Popup per-marker sizing lost the selection (owner-reported):** *root cause:* finishing a pointer interaction on a dot rebuilds both dot layers (`pointerup` → `renderMmMarkers()`), detaching the dot under the cursor — the browser then delivered the follow-up `click` to the canvas beneath, which ran place + auto-advance and re-pointed `currentSelectedStep` at the next unplaced step, so the marker the user had just tapped to select was deselected before they could touch the size slider. Fix: `genSuppressClick` / `mmSuppressClick` flags swallow exactly the one canvas click that follows a dot rebuild (150 ms window); `setMarkerSize` now refreshes BOTH surfaces (inline layer + popup layer when open) so per-marker resizing is visible in the popup; popup clicks within a placed marker's radius (`rad = max(12, size)/2 + 3 px`) select that step instead of placing a new pin (no more near-miss hijacks). | Resolved | Product Builder Step 2 (inline panel + large popup) — `product-builder.js` |
| D-059 | 2026-10-27 | **Step-3 product summary image opens large (owner request):** the summary card's thumbnail (`#summary-image-preview`) now has a dark eye overlay button (`#btn-summary-view-image`, absolute top-right) and is itself clickable (`cursor:zoom-in`); both open the shared zoomable `#image-view-modal` (`openSummaryViewer`: guards on visibility + non-empty src, cache-busts the src, `resetViewZoom()`). `refreshStep3Summary` shows/hides the eye button together with the image, so gallery add/remove and entering Step 3 (`showFlowStep` step 3) keep the button in sync; placeholder state shows no button. | Resolved | Product Builder Step 3 (Product Summary card) — `Builder.cshtml`, `product-builder.js` |
| D-060 | 2026-10-27 | **Marker assignment is compulsory (owner request):** a bolt step without a placed pin (X+Y) now blocks BOTH "Generate & Add to Product" and any Step 2 → Step 3 navigation (`goToStep(3)`, incl. the top stepper nav): the flow bounces back to Step 2 (`applyStep(2)` when needed), selects + scrolls to the first unmarked row, lists it by name in a step-2-owned alert, and paints every unmarked row `table-danger` until `updateRowMarker` (placement/drag) or the remove-image cleanup fixes it. `firstUnmarkedGenRow` / `refreshUnplacedMarkers` / `focusUnplacedMarker` helpers added near `collectGenSteps`; escape hatch: delete all draft rows via the row x-buttons. `populateQuickSteps(4,…)` at init is now conditional on `visibleRows().length === 0` so edit mode with attached templates keeps an empty draft generator and stays freely navigable (quick-steps sidebar still seeds on demand). | Resolved | Product Builder Step 2 (generator + navigation gates) — `product-builder.js` |

---

## Open Questions (ask the owner)

| ID | Date | Question | Blocks | Status |
|---|---|---|---|---|
| Q-01 | 2026-10-12 | What does each instrument send over USB (HID keyboard, USB-serial/CDC, vendor protocol)? Provide a sample capture. | P6.3, P6.4 | Open |
| Q-02 | 2026-10-12 | Which ESP32 model/USB host option will be used? | P6.4 | Open |
| Q-03 | 2026-10-15 | Real plant BM columns provided (`Sales Order`, `Item Code`, `Finished Good Item Desc`, `Qty.Reqd`, `Due Date`, etc.) and plant colours: Black (1), Red (2), Yellow (3), Green (4), Blue (5), White (6). | P4.3, P4.4 | Resolved |
| Q-04 | 2026-10-12 | Confirm D-021: is `(ProductCode, DueDate)` the right identity of a BM row on re-upload? | P4.10 | Open |
| Q-05 | 2026-10-12 | Confirm D-022: one device per worker, or shared per station? | P5.4 | Open |
| Q-06 | 2026-10-12 | Confirm D-023: peak torque and caliper hold/button vs stable-for-N-ms for acceptance? | P6.3 | Open |
| Q-07 | 2026-10-12 | Do jobs need individual product serial numbers per unit (one serial per unit) or per job only? | P4.6, P7.3 | Open |
| Q-08 | 2026-10-12 | Is there a single plant, or multiple sites to support? | Schema | Open |
| Q-09 | 2026-10-12 | Which time zone and date format should the UI use? | P1.8 | Open |
| Q-10 | 2026-10-12 | What is the deployment target (IIS vs Kestrel service) and SQL Server edition? | P8.7 | Open |
| Q-11 | 2026-10-19 | Confirm D-045: everyone accepts that the MaxRetries→Rework rule (PRD WRK-7, Architecture §7.5) is replaced by unlimited retries + operator-recorded defects (`ForceFail`)? | P5.9 docs, P8.4 rework workflow | Open |

---

## Session Log

Append one entry per session. Newest at the bottom.

### Template
```
### Session N � YYYY-MM-DD � <agent/person>
- Goal:
- Done: (task IDs and what changed)
- Decisions added: (D-nnn)
- Tests run: (what, result)
- Problems / surprises:
- Next step: (exact next task ID and the first thing to do)
```

### Session 0 � 2026-10-12 � Documentation
- Goal: Convert the project spec into the six working documents.
- Done: Created `PRD.md`, `Architecture.md`, `Rules.md`, `Phases.md`, `Design.md`, `Memory.md`. Recorded decisions D-001 to D-027 (gaps in the spec resolved or marked provisional).
- Decisions added: D-001 to D-027.
- Tests run: none (no code).
- Problems / surprises: The spec has a few inconsistencies, resolved as D-007 to D-011.
- Next step: P1.1 create the solution and projects.

### Session 1 � 2026-10-12 � Phase 1 Foundation
- Goal: Runnable solution with login, roles, theme, and layout.
- Done: P1.1 through P1.9 complete. Created solution and projects (`Web`, `Core`, `Services`, `Data`, `Tests`, `Tools.Simulator`). Identity with `ApplicationUser`, SQL Server connection, initial migration applied. Roles `Admin` and `Worker` seeded, admin user seeded. Theme tokens, `_AdminLayout` and `_WorkerLayout`. Admin dashboard shell, Worker job board shell. Serilog logging, `/health` endpoint, TimeProvider.
- Tests run: Built and verified.
- Next step: P2.1 start Phase 2 (Templates).

### Session 2 � 2026-10-13 � Phase 2 Templates Complete
- Goal: Implement all Phase 2 template management features (TPL-1 through TPL-10).
- Done: P2.1 through P2.10 complete.
  - P2.1: Entities `Template`, `TemplateStep`, Enums `InstrumentType`, `TemplateMode`, `ToleranceMode`, migration `AddTemplatesAndAuditLog` applied.
  - P2.2: `ToleranceService` with 17 unit tests for band calculation (absolute, percent, inclusive bounds, MidpointRounding.AwayFromZero).
  - P2.3: `ImageUploadService` with JPG/PNG validation, magic bytes, <= 5MB limit, GUID filename under `uploads/templates/`.
  - P2.4 - P2.6: 3-stage wizard (`WizardStage1.cshtml`, `WizardStage2.cshtml`, `WizardStage3.cshtml`) with visual mode selection, AJAX image upload, click-to-place markers (% X/Y), per-step instrument selector, target & tolerance bounds.
  - P2.7: `Detail.cshtml` preview showing calculated bands per step, image markers rendered at exact coordinates, and version history.
  - P2.8: `Index.cshtml` template list with search, mode/instrument/status filters, pagination, and activate/deactivate toggle.
  - P2.9: Versioning logic in `TemplateService` (used templates create new version with incremented Version and updated `IsLatest`; unused edit in place; all versions viewable). Tested with unit tests.
  - P2.10: `AuditLog` table created in migration and audit records logged for template creation, updates, new versions, and activations.
  - Seeded 6 initial templates for 6-panel locker (`LK-BASE`, `LK-LEFT`, `LK-RIGHT`, `LK-BACK`, `LK-TOP`, `LK-DOOR`) for Phase 3 readiness.
- Tests run: 24/24 passing (17 Tolerance evaluator tests + 7 TemplateService tests).
- Problems / surprises: Fixed nullable warning in `Detail.cshtml`, handled null `TemplateCode` gracefully in `IsCodeUniqueAsync`, applied `dotnet format`.
- Next step: P3.1 start Phase 3 (Products: compose templates into products).

### Session 3 � 2026-10-13 � Phase 3 Products Complete
- Goal: Implement all Phase 3 product management features (PRD-1 through PRD-7).
- Done: P3.1 through P3.7 complete.
  - P3.1: Entities `Product`, `ProductComponent`; `AppDbContext` config + migration `AddProducts` (applied). Unique `ProductCode`, non-unique `(ProductId, AssemblyOrder)` index (D-029).
  - P3.2/P3.3: Single builder screen (`Views/Product/Builder.cshtml` + `_ComponentRow.cshtml` + `wwwroot/js/product-builder.js`) with code/name/description/image and dynamic component rows (add from latest templates, quantity, drag / arrow reorder). Atomic save, requires =1 component, allows reuse of the same template (D-028).
  - P3.4: `Views/Product/Detail.cshtml` � components in assembly order as expandable accordions showing each step's target, accept band and tolerance mode, plus template version and a "newer version available" flag.
  - P3.5: `ProductService.CloneAsync` (new unique `-COPY` code, copies components); wired to list + detail.
  - P3.6: `Views/Product/Index.cshtml` � list with search (name/code), active/inactive filter, pagination, activate/deactivate, clone, edit.
  - P3.7: `ProductService.PreviewUpdateToLatestAsync` / `ApplyUpdateToLatestAsync` with `Views/Product/UpdateComponent.cshtml` showing an old-vs-new step diff before applying (Architecture �11).
  - `ImageUploadService` generalized for `uploads/products/` (D-030). Seeded the reference product `LK-6P` (Locker 6-Panel, 6 components).
- Decisions added: D-028, D-029, D-030.
- Tests run: 36/36 passing (17 tolerance evaluator + 7 template service + 12 product service). `dotnet format --verify-no-changes` clean. UI exercised over HTTP at 127.0.0.1:5084 while logged in as admin: list, detail, create builder, edit builder, update-component preview all HTTP 200; a POST create round-tripped to the detail page (then the smoke-test row was removed).
- Problems / surprises: (1) `dotnet run --no-launch-profile` defaults to Production, so user-secrets (`Seed:AdminEmail/Password`) are not loaded and the admin seed is skipped � use `--launch-profile http` for Development. (2) On Windows, `localhost` did not reach Kestrel here; use `127.0.0.1`. (3) `sqlcmd` (ODBC 18) needs `-C` to trust the local certificate.
- Next step: P4.1 � add `BM`, `ColourMap`, `Job`, `Notification` entities and seed.

### Session 4 � 2026-10-13 � P4.1 BM and Jobs schema
- Goal: P4.1 � BM, Colour map, Job, Notification entities, migration and seed.
- Done: P4.1 complete.
  - Added enums `PriorityLevel` (Black 1 � White 5) and `NotificationType`; `JobStatus`, `AssignmentMode`, `ExecutionMode`, `BmUploadStatus`, `BmRowImportStatus` already existed.
  - Entities `BmUpload`, `BmRow`, `PriorityColourMap`, `Job` (`Quantity`, `RowVersion`, `ExecutionMode`), `Notification`.
  - `AppDbContext`: DbSets + config � unique `JobNo`, indexes `Jobs(Status, Priority, DueDate)` and `Jobs(AssignedWorkerId, Status)` per �5.4, `BmRows(UploadId)` and `BmRows(ProductCode, DueDate)` for the re-upload rule, `Notifications(CreatedAt)`; FKs Product `Restrict`, worker / source BM row / notification job `SetNull`, BM rows cascade from their upload.
  - Migration `AddBmAndJobs` applied to SQL Server (verified `Jobs.RowVersion = timestamp`, `DueDate = date`, enums `tinyint`).
  - Seeded the 5 colour-map entries (`#000000/1 Black`, `#FF0000/2 Red`, `#FFFF00/3 Yellow`, `#00B050/4 Green`, `#FFFFFF/5 White`, tolerance 40).
- Decisions added: D-031.
- Tests run: 36/36 passing (schema task � no new service logic yet; parser/colour/priority tests arrive in P4.3, P4.4, P4.7, P4.10, P4.11). `dotnet format --verify-no-changes` clean. App started against SQL Server, `/health`, `/Product` and `/Template` all 200.
- Problems / surprises: EF logged 5 "database-generated default but no sentinel value" warnings for the enum columns. Fixed by removing the redundant `HasDefaultValue` calls (the C# properties already carry the same defaults) and adding migration `DropRedundantEnumDefaults`; startup is now warning-free for this. Also: EF column defaults on byte-backed enums are misleading (`0` is not a valid member), so enum columns are declared `tinyint` only.
- Next step: P4.2 � colour map admin screen (add/edit/delete shades, priority, label, match tolerance).

### Session 5 � 2026-10-13 � P4.2 Colour map admin screen
- Goal: P4.2 � Admin screen to add / edit / delete shades, with priority, label and match tolerance (BM-3, US-A5).
- Done: P4.2 complete.
  - `IColourMapService` / `ColourMapService`: list (optionally inactive), get, `GetActiveAsync` for the parser, create, update, soft delete. Validates `#RRGGBB`, priority 1�5, label = 30, tolerance 1�441; normalises hex to upper case and rejects duplicate hex.
  - Guard: the last active shade cannot be deactivated (the importer must always have something to match against).
  - `ColourMapController` (`/ColourMap`, Admin-only) with Index, Edit (reuses the same page), Create, Update, SetActive. Service errors re-render the form so entered values are never cleared (Design �5.2).
  - Views `Views/ColourMap/Index.cshtml` (create/edit card + shades table) and `_Form.cshtml`; priority badges reuse the existing `badge-priority` / `badge-pri-1..5` classes with shape icons and a text label (Design �5.5); swatch + hex per Design �7.6.
  - 15 new tests (`ColourMapServiceTests`).
- Decisions added: D-032, D-033.
- Tests run: 51/51 passing. `dotnet format --verify-no-changes` clean. UI exercised over HTTP while logged in as admin: GET list (200, 5 seeded shades, all 5 badge variants), create ? 302 + flash + row, Edit ? 200 with values prefilled, Update ? 302 and persisted, SetActive ? 302 and inactive badge shown. The smoke-test shade was deleted afterwards; palette restored to exactly the 5 seeded entries.
- Problems / surprises: Design �6.1 fixes the sidebar to 8 items, so the screen cannot get a nav entry � recorded as D-032, with a link planned from the BM upload page in P4.5. Run the dev server with `ASPNETCORE_ENVIRONMENT=Development` so user-secrets (admin credentials) load; otherwise the seeder warns and skips the admin.
- Next step: P4.3 � `BmImportService` parser (configurable header map, row reading, quantity/date parsing, product-code lookup) with fixture-based tests.

### Session 6 � 2026-10-13 � P4.3 BM parser
- Goal: P4.3 � BM Excel parser: configurable header mapping, row reading, quantity/date parsing, product-code lookup, with fixture-based tests (BM-2, Architecture �7.3).
- Done: P4.3 complete.
  - Added `ClosedXML` 0.102.3 to `SmartTorque.Services` and wrote `BmImportService` / `IBmImportService`.
  - Types: `BmHeaderMap` (ProductCode / Quantity / DueDate / Remarks, defaults `ProductCode` etc.), `ParsedBmRow`, `BmParseResult` (with `HasFatalError`, `OkCount`, `ErrorCount`, `Summary`), `BmSettings.HeaderMapKey`.
  - `GetHeaderMapAsync` reads the `BmHeaderMap` JSON setting and falls back to the default when absent **or corrupt**. `MapColumns` matches headings trimmed and case-insensitively (�7.3 step 2), first match wins.
  - Row reading: first worksheet, header row = first used row, blank rows skipped, quantity accepts a number or numeric text (whole numbers > 0), due date accepts a real date, an Excel serial or text, solid RGB fill of the product-code cell captured.
  - Product-code lookup: single batched query, flags unknown (`was not found`) and inactive (`is not active`) products. Errors are accumulated per row so one row can report several problems.
  - File-level failures are non-throwing: non-.xlsx file, no worksheets, empty sheet, missing product-code column.
  - **Nothing is written** � the preview screen persists and confirm-import creates Jobs (P4.5 / P4.6).
  - Fixtures committed: `bm-default-headers.xlsx` (4 usable rows incl. text date + Excel serial + a blank row), `bm-custom-headers.xlsx` (6 rows: valid, unknown product, qty 0, qty text, bad date, black-filled code), `bm-empty.xlsx`. Copied to output via `Fixtures\**\*.xlsx` in the test csproj.
  - 19 new tests (`BmImportServiceTests`).
- Decisions added: D-034, D-035.
- Tests run: 70/70 passing. `dotnet format --verify-no-changes` clean. No HTTP UI this task � the parser has no screen yet (P4.5 adds it).
- Problems / surprises: ClosedXML 0.102 renamed `IXLFill.FillType` ? `PatternType`, and moved `DataType` / `GetDouble` / `GetDateTime` onto `IXLCell` rather than `XLCellValue` � the earlier API guesses failed to compile. `ReadText` reads `cell.Value.ToString()` rather than `GetString()`, because ClosedXML throws when stringifying some cell types. A ternary of the form `cond ? [] : await ...ToListAsync()` will not compile (a collection expression cannot be target-typed through `var`).
- Next step: P4.4 � colour detection (RGB, theme, tint, indexed, no fill ? White) and nearest-colour matching with tolerance.

### Session 7 � 2026-10-13 � P4.4 Colour detection and matching
- Goal: P4.4 � resolve a cell fill to RGB (RGB / theme + tint / indexed / no fill) and match it to the nearest shade within tolerance (BM-4, BM-5, Architecture �7.3 step 4).
- Done: P4.4 complete.
  - `BmColourDetector` (static): `Detect(workbook, cell)` ? `#RRGGBB`, never throwing � an unreadable fill degrades to White instead of failing the import. Branches on `XLColorType` (`Color` / `Theme` / `Indexed`); theme uses `IXLTheme.ResolveThemeColor` + `XLColor.ThemeTint`, indexed uses the `XLColor.IndexedColors` palette (65 entries, index 64 = auto). `ApplyTint` implements ECMA-376; `DefaultWhite` is `#FFFFFF`.
  - `ColourMatcher` (static): `Distance` (Euclidean RGB), `Resolve(hex, map)` ? `ColourMatch(Shade, WithinTolerance)` with `Priority`. Each shade is judged against **its own** `MatchTolerance`; nothing within tolerance falls back to the White shade (BM-5).
  - `BmImportService` now loads the active colour map once per parse, calls the detector per row and stamps `ParsedBmRow.Priority`. An unrecognised colour raises a **Warning** row (still importable) rather than being silently defaulted.
  - Replaced P4.3's `ReadSolidFill`; P4.3's two colour tests were updated from `null` to `#FFFFFF`, and two new end-to-end tests assert a black-filled row comes out `Priority = Black`.
  - 16 new `ColourMatcherTests` + 32 new `BmColourDetectorTests` (the latter round-trip real workbooks through ClosedXML in memory).
- Decisions added: D-036.
- Tests run: 118/118 passing. `dotnet format --verify-no-changes` clean. No HTTP UI this task � the parser still has no screen (P4.5).
- Problems / surprises: **ClosedXML's `XLColor.ThemeColor`, `.ThemeTint` and `.Indexed` all throw** if read while `ColorType` is not `Theme` / `Indexed`, so the type must be checked first � `ReadSolidFill` in P4.3 was silently relying on a `catch`. Also `XLColor.IndexedColors` holds `XLColor` (not `Color`) values, and `XLColor.FromRgb` does not exist (use `FromArgb`). Two of my own test expectations were wrong and were corrected rather than the code: ECMA tint is discontinuous at exactly 0.5 (branch boundary returns the original channel, not white), and `#050505` is 8.66 from black, outside a tolerance of 5. A reflection probe app in `%TEMP%` was used to discover the real API instead of guessing; it has been deleted.
- Q-03 (a real supplier BM sheet with real colours/headers) is still **Open** � P4.3/P4.4 are validated against generated fixtures only, so the tint semantics and real header names remain assumptions until a real file arrives.
- Next step: P4.5 � upload page + preview screen (drop zone, colour legend panel, preview table with swatch / priority badge / status / message, per-row override and Skip, summary strip).

### Session 8 � 2026-10-13 � P4.5 BM upload and preview screens
- Goal: P4.5 � upload a supplier workbook and review every parsed row, with per-row correction, before anything is created (PRD BM-7, BM-8; Design �7.6; Architecture �7.3 steps 1�5).
- Done: P4.5 complete.
  - `IBmFileStore`/`BmFileStore` (Services): saves `.xlsx` only, =10 MB, to `wwwroot/uploads/bm/{guid}.xlsx`, plus `OpenRead` and a traversal-guarded `Delete` that refuses anything outside the uploads root. It is separate from `ImageUploadService`, whose magic-byte check is JPG/PNG-specific (D-030 still holds for its original call site).
  - `IBmPreviewService`/`BmPreviewService`: `UploadAsync` stores the file **first**, re-reads it from disk, parses, then persists `BmUpload` + `BmRow`s � `Previewed` on success, `Failed` with zero rows when the workbook is unreadable. Also `GetAsync`, `RecentAsync`, `ReadFileErrorsAsync` (re-parses the stored file to recover file-level errors � no schema change), `SetRowPriorityAsync`, `SetRowSkippedAsync`, `CancelAsync` (drops rows, upload and file; refused once `Confirmed`).
  - `BmController` (`[Authorize(Roles="Admin")]`): `Index`, `Upload`, `Preview`, `SetRowPriority`, `SetRowSkipped`, `Cancel`. Preview is 404 for an unknown id and challenge-redirects anonymous callers.
  - Views: `Views/Bm/Index.cshtml` (upload card + colour legend + recent-uploads table + empty state), `Views/Bm/Preview.cshtml` (header, summary strip `4 rows � 4 OK � 0 warnings � 0 errors`, legend, failed-file alert, preview table with swatch + hex, priority select, status badge, per-row message, Skip/Restore), and shared `Views/Shared/_ColourLegend.cshtml` which carries the **"Edit colour map"** link (D-032/D-037).
  - `theme.css` gained `.table-row-fail` / `.table-row-warn` / `.table-row-skip`, all token-only (`--fail-bg`, `--warn-bg`) � no hex literals were added to any view.
  - 23 new `BmPreviewServiceTests`: upload round-trip to disk, failed-upload marking, rejection of non-xlsx / oversized / empty files with no DB or disk residue, row ordering, priority override + range + unknown-row, skip/restore for both an Ok and a Warning row, refusal to skip an Error row, refusal to edit or cancel a `Confirmed` upload, cancel removing rows and file, and the three `ReadFileErrorsAsync` paths. The tests use the **real** `BmFileStore` against a temp wwwroot and the real parser against the committed fixtures.
- Decisions added: D-037 (routes + colour-map entry point), D-038 (store-before-parse, re-read errors, skip only importable rows), D-039 (native confirm, Design �5.8 modal deferred).
- Tests run: 141/141 passing. Build 0/0. `dotnet format --verify-no-changes` exit 0.
- UI exercised over HTTP as admin (tablet layout uses `col-lg-*` stacking below 992 px and `table-responsive` for horizontal scroll): `/Bm` renders form, legend and empty state ? upload redirects to `/Bm/Preview/1` with all four rows OK ? priority override to Black persists ? Skip gives `4 rows � 3 OK � 0 warnings � 0 errors � 1 skipped` with `.table-row-skip` and a Restore button ? Restore returns to 4 OK ? `bm-custom-headers.xlsx` with no header map yields a **Failed** preview whose message was recovered from the file ("Could not find a product code column in row 1�") ? `bm-empty.xlsx` yields Failed with "The first worksheet is empty." ? deactivating the White shade turns all four rows **Warning** (`table-row-warn`, warning icon, `badge-onhold`) ? reactivating White and cancelling all four uploads returns `/Bm` to its empty state with `uploads/bm` left empty. `/Bm/Preview/999` ? 404; anonymous `/Bm` ? 302 to login; the legend's "Edit colour map" link resolves to `/ColourMap` (200). All test uploads were cleaned up and the colour map restored.
- Problems / surprises: the login endpoint is **`/Account/Login`**, not `/Identity/Account/Login` � the latter 302-challenges and silently ate the first two login attempts. Antiforgery rejects a token read from a different page than the POST, so each form is re-read immediately before posting. `bm-empty.xlsx` is fatal at the worksheet level rather than a headings-only sheet, so the preview's zero-row empty state was never reached.
- Known issue added: K-12 (zero-row preview state unexercised).
- Next step: P4.6 � confirm import: create the Jobs in one transaction, generate `JobNo`, set `Quantity`, raise the Black-job notification.

### Session 9 — 2026-10-14 — P4.6 confirm import and P4.7 priority ordering
- Goal: P4.6 + P4.7 — turn a confirmed preview into Jobs atomically, raise the Black-job notification, and order the workboard (PRD BM-9; Architecture §7.3 step 5, §7.2).
- Done:
  - `JobImportService.ConfirmAsync`: in one `SaveChanges` — `JobNo` generation (`JOB-{yyyyMMdd}-{nnnn}` per upload), Jobs from the importable rows (priority / due date / quantity carried over, `SourceBmRowId` set for the re-upload rule), rows consumed, upload → `Confirmed`, audit entry, and one Black-job `Notification` targeted at the `Worker` role through `INotificationService` (a single notice per import, not one per job).
  - `GetJobNumbersAsync` so the preview can list the jobs it produced; refusal paths (unknown, failed or already-confirmed upload, zero importable rows) write nothing.
  - `PriorityService` (pure, singleton per Architecture §7.2): `OrderForWorkboard` = Priority asc → non-null DueDate first → DueDate asc → SourceBmRowId tie-break, used by the workboard query.
  - Tests for confirm (jobs created with the right fields, numbering, notification raised, atomic refusal paths) and for the ordering rule.
- Decisions added: none new — D-031, D-036, D-038 applied as written.
- Tests run: 165 total, 163 passing — the 2 failures were duplicate `LK-6P` seed rows tripping `ConfirmAsync`'s `ToDictionaryAsync(p => p.ProductCode)`; at the time they were misattributed to the ProductCode index drift (K-13) and left open. `dotnet format --verify-no-changes` clean.
- Problems / surprises: the test failures looked like the code-model/DB index drift because both involve `ProductCode`; the real cause only became clear when the seeds were inspected (fixed in Session 10).
- Next step: P4.8 — workload board at `/Bm/Workload` (D-037): filter bar, Black pinned with the 4px left border, service-level query.

### Session 10 — 2026-10-14 — P4.8 workload board, EF scaffold, K-13 fix
- Goal: close the P4.8 gaps (board UI, filters, tests), generate the EF models the owner asked for (D-040), and clear the gates.
- Done:
  - `JobImportService.GetWorkloadAsync` + `WorkloadFilter` / `WorkloadResult`: filter (priorities, inclusive due range, status, worker, unassigned-only), total count, ordered page with Product + AssignedWorker includes. The query lives in the service because Architecture §4 keeps controllers off the DbContext; `PriorityService` stays pure and is injected for the ordering.
  - `Views/Bm/Workload.cshtml`: filter card (priority chips as `btn-check` + `badge-priority wl-chip`, due-from/to date inputs, worker/status selects with round-tripped selected values, Apply + Reset), 8-column job table, Black row = `row-critical` (4px `--pri-black` left border) + "Critical" tag and pinned first, `product code / qty / due / worker / status badge` cells, disabled Assign/Hold/Cancel buttons titled "arrives in P4.9 / P4.11", pagination links that rebuild the query string (the GET form drops `page`).
  - `BmController.Workload`: worker dropdown from `UserManager.GetUsersInRoleAsync("Worker")` plus an `open` sentinel for unassigned jobs (Identity infra, not DbContext — Architecture §4), `yyyy-MM-dd` / status parsing, and page clamping: `page < 1` → 1, `pageSize` → 1..200, a stale bookmark past the end re-queries the last page instead of showing an empty table.
  - `theme.css`: `.wl-chip` (unticked chips at 50% opacity, checked at full, focus ring) and `.row-critical > td:first-child` border — token-only, no hex literals.
  - 6 new `GetWorkloadAsync` tests → 171 total: Black-first ordering, priority-tick filter, inclusive due range with null-DueDate exclusion, status filter, open/worker filter, pagination + totals + page clamping.
  - D-040 scaffold: `dotnet ef dbcontext scaffold … --output-dir Models --namespace SmartTorque.Web.Models --context SmartTorqueDb --no-onconfiguring` → 17 POCOs + context in `SmartTorque.Web/Models`; all DbSets given `= null!` because `dotnet format` reports CS8618 where csc does not, and CS8618 is an error via `WarningsAsErrors`.
  - Test seeds made idempotent (skip existing codes/hexes) → 165/165; root cause of the 2 old failures: re-upload tests seeding `LK-6P` twice → `ToDictionaryAsync` "same key already added".
  - Mojibake repair: 12 double-encoded comment characters across 4 `.cs` files (`Â§` → `§`, `â€"—` → `—`), BOMs preserved; grep for `Â|â€` now returns nothing.
  - **K-13 fixed**: re-added `entity.HasIndex(p => p.ProductCode).IsUnique()` to `AppDbContext`. The app had refused to boot (`PendingModelChangesWarning` thrown by `MigrateAsync`) and `dotnet ef database update` failed, while migration `AddProducts`, the model snapshot and the live DB all still carry `IX_Products_ProductCode` (unique), the P3.1 record says "Unique ProductCode", and `ConfirmAsync` keys a dictionary on it — so the code model was the only side out of step. `dotnet ef migrations has-pending-model-changes` now reports none.
  - UI exercised over HTTP as admin at tablet-friendly markup (`table-responsive`, `container-fluid`, `col-md-*`/`col-lg-*`, viewport meta): the full real flow — login → upload `bm-default-headers.xlsx` → preview (4 rows OK) → `SetRowPriority` on row 1 → Confirm ("4 jobs created (1 Black, notification raised: True)") → `/Bm/Workload` with 17/17 structural checks. Every filter verified against real data: priorities 1 / 5 / 1+5 / 2+3, `worker=open`, status 1 and 5, inclusive due windows (20 Oct..05 Nov → 3; ..27 Oct → 2; dueTo 2025-12-31 → the single 2025 job), combined filter with all three states round-tripped, `page=0`/`page=99` clamps, `pageSize=0` pager. DB verified: 1 Black + 3 White Pending jobs, Worker notice "Urgent Black job JOB-20261014-0001 needs a worker.", 1 audit row; the confirmed preview shows "Imported 4 job(s)" + the job list and no Confirm button.
  - Added `Dockerfile` + `.dockerignore` + `docker-compose.yml` at the repo root (multi-stage `sdk:9.0` → `aspnet:9.0`, non-root `$APP_UID`, `curl` HEALTHCHECK on `/health`; compose stacks web with SQL Server 2022 gated on a `service_healthy` check — sqlcmd login probe with bash TCP fallback — publishes SQL on host port **14330** to avoid clashing with the local 1433 instance, and seeds admin via `Seed__*` env vars). Validated earlier with a local Release `dotnet publish` (exit 0) + PyYAML parse of the compose file.
  - Added `README.md` at the repo root (moved there from `SmartTorque/README.md` at the owner's request; every path in it still resolves because Step 1 does `cd AssemblIQ/SmartTorque`): simple step-by-step clone-and-setup guide (prerequisites + `dotnet-ef`, clone `https://github.com/ManglamX/AssemblIQ.git` then `cd AssemblIQ/SmartTorque`, user-secrets, build, run on :5084, login `admin@smarttorque.local` / `Admin@12345`, optional manual DB, Docker, tests, short troubleshooting). Docs (`PRD.md` etc.) live in the repo's root folder, one level above the solution.
  - GitHub: `https://github.com/ManglamX/AssemblIQ` is **public**, default branch `main`, single commit; its root contains `.vscode/`, `SmartTorque/` and `Architecture.md`, `Design.md`, `Memory.md`, `PRD.md`, `Phases.md`, `Rules.md` - Owner **pushed** it: `main` now has 2 commits, the root `README.md` renders as the repo landing page, and `SmartTorque/README.md` is gone (verified via raw.githubusercontent + the repo file listing). The local `AssemblIQ/` workspace **is** a git repo (`main` tracking `origin` = the GitHub repo), so it is the checkout the owner pushes from. Removed `SmartTorque/godrejlogo.png` at the owner's request (stray duplicate at the solution root - the app serves `src/SmartTorque.Web/wwwroot/images/godrejlogo.png` via `~/images/godrejlogo.png` in `Views/Shared/_AdminLayout.cshtml`).
  - Docker end-to-end **verified**: `docker build -t smarttorque .` succeeded (543 MB; the BuildKit NuGet cache mount + `--disable-parallel` got `dotnet restore` through in ~9 min despite retries), `mcr.microsoft.com/mssql/server:2022-latest` pulled, `docker compose up -d --no-build` started `smarttorque-db-1` + `smarttorque-web-1`, both **healthy**. Verified over HTTP on :8080: `/Account/Login` 200 → real admin login (302) → `/Admin/Dashboard` 200, `/health` Healthy, `/Bm` 200; migrations + seeding ran in-container (roles, admin, 7 settings, 6 templates, LK-6P + 6 components, 5 colour-map entries). Host `sqlcmd -S localhost,14330 -U sa -C -d SmartTorqueDb` sees 19 tables / 6 templates / 1 user. Earlier build failures were purely the flaky host network (nuget.org SSL EOFs, Docker Hub TLS timeouts) - no Dockerfile/compose defects.
  - Dockerfile `restore`/`publish` steps use a BuildKit NuGet cache mount (`--mount=type=cache,target=/root/.nuget/packages`) plus `--disable-parallel`, so partial downloads survive a failed attempt and fewer sockets are opened. Keep this - it is what made the build converge on this network.
- Decisions added: D-040 (EF scaffold is reference-only; the app keeps `AppDbContext`).
- Tests run: 171/171 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.
- Problems / surprises: (1) K-13 above blocked app startup — the old header's "2 known failures from Q-04" conflated two unrelated things (duplicate test seeds vs. index drift). (2) One new ordering test expectation was wrong, not the service: priority is the primary sort, so a Red job precedes a White job regardless of due date — the assertion was corrected. (3) The write tool hit ECONNRESET twice on `Workload.cshtml`; a retry succeeded. (4) `.editorconfig` sets `insert_final_newline = false` for `*.cs`, so `dotnet format` flagged a final newline I had added. (5) The database held no BM data (baseline seed only) — earlier uploads/jobs are gone — so the exercise re-ran the real import flow to populate the board. (6) After D-040, `dotnet ef` discovers two contexts: pass `--context AppDbContext`. (7) PowerShell 5.1 has no `Invoke-WebRequest -Form`; multipart went through `curl.exe` with a cookie jar. (8) This file itself has 156 `U+FFFD` replacement chars from an earlier encoding mishap (e.g. section signs and arrows); historical lines were left as-is rather than guessed at — see K-14.
- Next step: BM extraction enhancements (item code discovery, prompt for description column, Blue colour priority).

### Session 5 — 2026-10-15 — Antigravity
- Goal: BM Excel extraction enhancements for plant production sheets:
  1. Auto-discover `Item Code` as the common item code column across BMs.
  2. Ask for the column name of the description before rendering the Excel preview.
  3. Support all 5 plant colours directly from Excel: Black, Red, Yellow, Green, and Blue (plus White fallback).
- Done:
  - `Enums.cs`: Added `Blue = 5` and updated `White = 6` in `PriorityLevel` enum.
  - `BmColourDetector.cs`: Overloaded `Detect(workbook, cell, row = null)` to inspect row style fill (`row.Style.Fill`), first cell (`row.Cell(1)` / Sales Order), and used cells across the row if the `codeCell` is unstyled or default white.
  - `BmImportService.cs`: Added `InspectHeadersAsync` to discover all columns and detect `Item Code`, suggested description (`Finished Good Item Desc`), quantity (`Qty.Reqd`), and due date (`Due Date`). Added `ItemCodeAliases` (`["Item Code", "ItemCode"]`) and updated overloads for `ParseAsync` to accept custom column maps and row context.
  - `BmPreviewService.cs`: Added overloads accepting custom column maps; updated priority validation range to 1..6.
  - `ColourMapService.cs`: Updated priority validation range check to 1..6.
  - `DataSeeder.cs`: Updated `SeedColourMapAsync` to seed Blue (`#0070C0` and `#4472C4` theme blue at Priority 5, tolerance 60) and White (Priority 6), plus dynamic migration for existing databases.
  - `BmController.cs`: Added `InspectHeaders(file)` AJAX endpoint; updated `Upload` to prompt for description column before rendering preview (modal on file select + fallback `MapColumns` view); added `ProcessPendingUpload`.
  - Views & CSS: Added interactive Bootstrap modal on `Bm/Index.cshtml`, `Bm/MapColumns.cshtml` fallback, Description column in `Bm/Preview.cshtml`, Blue priority badge/legend in `Bm/Workload.cshtml`, `_ColourLegend.cshtml`, `ColourMap/_Form.cshtml`, and `theme.css` (`--pri-blue: #0070C0`, `.badge-pri-5`).
  - Database: Updated existing `SmartTorqueDb` priorities so White is 6 and Blue is 5.
  - Tests: Added 3 new unit tests in `ColourMatcherTests.cs` and `BmImportServiceTests.cs`. All 174 tests passing.
- Decisions added: D-041 (Blue priority level 5, auto-detect Item Code, prompt for description column, row colour scan). Q-03 resolved.
- Tests run: 174/174 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.
- Problems / surprises: Excel planners apply background fill to the whole row or column A (`Sales Order`) rather than just `Item Code`. Scanning row fill and used cells resolves this reliably.
- Next step: Auto-generated template codes and duplicate detection.

### Session 6 — 2026-10-15 — Antigravity
- Goal: Deterministic auto-generated template codes & duplicate reuse detection:
  1. Remove liberty for the user to enter arbitrary template codes (user supplies Name, Description, Mode, and steps).
  2. Implement `TemplateCodeGenerator`:
     - Torque Wrench: `TW-{bolts}B-{torque}NM-{tolerance}` (e.g. `TW-4B-25NM-T2`).
     - Vernier Caliper: `VC-{sides}S-{dim}MM-{tolerance}` (e.g. `VC-2S-100MM-T0.5`).
     - Mixed Mode: `TW-{bolts}B-{torque}NM-{tol}-VC-{sides}S-{dim}MM-{tol}`.
  3. Real-time client-side & server-side duplicate check:
     - If matching template already exists, display existing template details (Name, Code, Version, Step count, targets/tolerances).
     - Direct action buttons: "Use This Template in Product Builder" (`/Product/Create?templateCode=...`) and "View Template Details".
     - Pre-populate product component and filter picker when navigating to Product Builder.
- Done:
  - Created `TemplateCodeGenerator.cs` in `SmartTorque.Services`.
  - Updated `Stage1ViewModel` and `WizardStage1.cshtml` to make `TemplateCode` auto-generated and read-only.
  - Updated `Stage3ViewModel` with `GeneratedTemplateCode` and `ExistingMatchingTemplate`.
  - Added AJAX endpoint `POST /Template/GenerateAndCheckCode` and server-side duplicate validation in `TemplateController.Save`.
  - Updated `WizardStage3.cshtml` with live code calculation, dynamic matching template alert card, and step breakdown table.
  - Updated `ProductController.Create` and `product-builder.js` to pre-populate product components and filter template picker when query string `templateCode` is supplied.
  - Added 9 unit tests in `TemplateCodeGeneratorTests.cs` (183 total tests passing).
- Decisions added: D-042 (Deterministic auto-generated template codes & duplicate template library reuse).
- Tests run: 183/183 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.
- Next step: P4.9 — manual and automatic assignment.

- **Session (2026-10-15)**:
  - Fixed Stage 2 to Stage 3 wizard navigation failure.
  - Root cause: In .NET 9 with `<Nullable>enable</Nullable>`, non-nullable `string TemplateCode` in `Stage2ViewModel` caused model validation to fail when `TemplateCode` was omitted/empty from Stage 1. Because `WizardStage2.cshtml` lacked a validation summary, the page silently re-rendered Stage 2.
  - Fix: Made `TemplateCode` nullable (`string?`) on `Stage2ViewModel` and `Stage3ViewModel`.
  - Added `ModelState.Remove(nameof(model.TemplateCode))` in `TemplateController.Stage2` and `Save`.
  - Added `<div asp-validation-summary="All">` alert in `WizardStage2.cshtml` so any validation failures are visible.
  - Removed invalid `<script>@Html.AntiForgeryToken()</script>` on line 186 in `WizardStage2.cshtml`.
  - Tests run: 184/184 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.

- **Session (2026-10-15 - Provision to view jobs based on item code and quantity across all BMs with product info link)**:
  - User requested: In BM section, have the provision to view jobs based on item code and quantity to be manufactured across all BMs (as item code is common), and clicking on it takes the user to the designed Product Info page (`/Product/Detail/{id}`).
  - Added `GetWorkloadByItemAsync(search)` to `IJobImportService` and implemented in `JobImportService.cs`, aggregating all jobs across BMs by `Product.ProductCode` with total quantity required, completed, remaining, job count, highest priority, earliest due date, and detailed breakdown of jobs.
  - Added `GetByCodeAsync(productCode)` to `IProductService` and `ProductService.cs`.
  - Added `DetailByCode(string code)` action to `ProductController.cs` and supported `productCode` in `ProductController.Create`.
  - Created `WorkloadByItemViewModel.cs` and `Views/Bm/ByItem.cshtml` with KPI cards, search filter, grouped item code cards with manufacturing progress, and detailed jobs list with clickable links to Product Info.
  - Created `_BmSubNav.cshtml` partial to provide seamless navigation between Workbooks, Workload Board, and Jobs by Item Code.
  - Linked `ProductCode` in `Workload.cshtml` (Workload Board table) and `Preview.cshtml` (BM preview table) to navigate directly to the designed Product Info.
  - Fixed Razor syntax issue in `Product/Detail.cshtml` (`v@(c.TemplateVersion)`).
  - Tests run: 186/186 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.

- **Session (2026-10-15 - P4.9 Manual and Automatic Assignment & Workload Board Actions)**:
  - User requested: Incorporate functionality directly into the Workload Board screen shown in the image (wire Assign, Hold, Cancel actions and auto-assignment).
  - Implemented `AssignJobAsync`, `AutoAssignJobsAsync`, `HoldJobAsync`, `ResumeJobAsync`, `CancelJobAsync` in `JobImportService.cs` with full audit trail.
  - Added endpoints in `BmController.cs`: `AssignJob`, `AutoAssign`, `HoldJob`, `ResumeJob`, `CancelJob`.
  - Updated `Views/Bm/Workload.cshtml`:
    - Wired interactive **Assign** button opening a modal with worker selection and "Leave open (unassigned)", plus priority override.
    - Added **Auto-Assign Open Jobs** button with workload balancing across active workers.
    - Wired **Hold** and **Resume** toggle buttons with confirmations.
    - Wired **Cancel** button with confirmation dialog.
    - Added dismissible TempData feedback alerts (`Success`, `Info`, `Error`).
  - Added 3 unit tests in `JobImportServiceTests.cs` (189/189 total passing).
  - Tests run: 189/189 passing. Build 0 warnings / 0 errors. `dotnet format --verify-no-changes` exit 0.

- **Session (2026-10-15 - Phase 5 Complete: Worker Portal, Simulators, Auditing & Summary)**:
  - User requested: Complete Phase 5 totally, everything in it.
  - Implemented all remaining Phase 5 features:
    - **P5.9 Retry & Fail Flow**: Enforces maximum retries per step (`MaxRetries = 3`). Exceeding retries marks the session Completed with `FinalResult = Fail` and transitions the parent Job to `JobStatus.Rework`.
    - **P5.10 Session Submit & Summary**: Added `GetSessionSummaryAsync(sessionId)`, session certificate view (`/Worker/JobBoard/Summary/{sessionId}`), full step-by-step measurement audit, tolerance deviation, status badges, and action button for next unit/component.
    - **P5.12 Dev Simulators**:
      - Interactive Web Simulator at `/dev/simulator` (`DevSimulatorController` and view) with torque gauge, caliper readout, slider controls, direct POST to `/api/readings`, and continuous ramp simulation.
      - CLI Simulator tool `SmartTorque.Tools.Simulator` with command-line arguments (`--server`, `--device`, `--key`, `--val`, `--stable`, `--ramp`, `--delay-ms`).
    - **P5.13 My History & Profile**: Added `GetWorkerHistoryAsync` and `GetWorkerProfileStatsAsync` in `SessionService`, `/Worker/JobBoard/History` with filters, and `/Worker/JobBoard/Profile` showing completed units, first-pass yield %, and active session stats.
    - **P5.14 Black-Job Alerts**: Added SignalR broadcast on BM import confirmation to `workers` group with toast/banner alerts on Worker Job Board.
  - Resolved EF Core InMemory INNER JOIN on `Worker` navigation in unit tests.
  - Handled unique index `IX_Sessions_JobId_ProductComponentId_UnitNo_ComponentInstanceNo` safely by resuming existing `InProgress` sessions.
  - Tests run: 221/221 passing. Build 0 warnings / 0 errors. Code formatted with `dotnet format`.

- **Session (2026-10-16 - Phase 6 Complete: Device API, Admin Management, ESP32 Firmware & Offline Buffer)**:
  - User requested: Complete Phase 6 totally, properly, without breaking earlier functionality.
  - Implemented all Phase 6 deliverables:
    - **P6.1 Device API Endpoints & Auth (`/api/device`)**:
      - Implemented `DeviceApiController.cs` at `/api/device` with `register`, `heartbeat`, `reading`, and `session/{transmitterId}`.
      - Enforced SHA-256 API key authentication (`X-Api-Key` header), rate limiting (20 req/s with ASP.NET Core `AddRateLimiter`), timestamp sanity checks, and unit validation (Nm for torque, mm for calipers).
      - Handled idempotency deduplication by `(DeviceId, seq)`.
      - Maintained `ReadingsApiController` at `/api/readings` for backward compatibility with earlier tools and tests.
    - **P6.2 Device Management UI (`/Device`)**:
      - Implemented `DeviceController.cs` and views (`Index.cshtml`, `Create.cshtml`, `Edit.cshtml`) with stats cards, instrument badges, and one-time secret API key display.
      - Real-time SignalR `deviceStatus` broadcast to `admins` group on heartbeats, registration, and activation toggle.
    - **P6.3 Stable-Value Rules**:
      - Integrated tolerance checks, peak torque release capture, and Vernier hold button validation.
      - Validated unexpected instrument type mismatch (returning 409 Conflict with hint broadcast to browser).
    - **P6.4 & P6.5 ESP32 Gateway Firmware (`firmware/esp32-gateway/`)**:
      - Implemented production C++ firmware for ESP32-S3 USB Host CDC/HID enumeration.
      - Built persistent sequence counter `seq` across reboots via NVS Preferences.
      - Built offline ring buffer (`offline_buffer.h`) storing up to 500 confirmed measurements when Wi-Fi drops, automatically replaying them chronologically upon reconnection before streaming new measurements.
      - Added PlatformIO build configuration and hardware wiring documentation (`README.md`).
    - **P6.6 LAN Latency Benchmark**:
      - Validated through automated benchmark test that reading ingestion, evaluation, and broadcast execute in < 30ms (well under the 500ms requirement).
    - **P6.7 Comprehensive Tests**:
      - Added `DeviceApiTests.cs` (13 tests) and `DeviceControllerTests.cs` (6 tests).
  - Tests run: 242/242 passing. Build: 0 warnings, 0 errors. Formatting: `dotnet format --verify-no-changes` exit 0.

---

## Known Issues

| ID | Date | Issue | Severity | Status |
|---|---|---|---|---|
| K-01 | 2026-10-13 | Services layer references the Data layer (`TemplateService` and `ProductService` inject `AppDbContext`), which violates Rules �3.1 (Services ? Core only). Carried over from Phase 2 to keep the existing pattern consistent. | Medium | Open |
| K-02 | 2026-10-13 | `wwwroot/css/theme.css` line 6 `@import`s Google Fonts at runtime, which violates the offline / no-web-fonts rule (D-016, Rules �2). | Medium | Open |
| K-03 | 2026-10-13 | Hardcoded hex colours outside `theme.css`: `site.css` (`#258cfb`) and `Views/Shared/_Layout.cshtml.css` (`#0077cc`, `#1b6ec2`). Rules �7.1. | Low | Open |
| K-04 | 2026-10-13 | `Program.cs` always runs `Database.MigrateAsync()` at startup; Rules �4.7 say migrations must not run in production. | Medium | Open |
| K-05 | 2026-10-13 | `AccountController.AccessDenied` returns `View()` but `Views/Account/AccessDenied.cshtml` does not exist. | Low | Open |
| K-06 | 2026-10-13 | Duplicate `ErrorViewModel` in `Models/` and `ViewModels/`. | Low | Open |
| K-07 | 2026-10-13 | Measurement columns are `decimal(18,4)` while the spec says `DECIMAL(10,3)`; reconcile in a later migration. | Low | Open |
| K-08 | 2026-10-13 | `Template` has no `RowVersion` concurrency token (Product also relies on implicit locking for now). | Low | Open |
| K-09 | 2026-10-13 | EF Core emitted a multiple-collection-include warning on the product detail query; fixed by adding `AsSplitQuery()` in `ProductService.GetByIdAsync`. | Low | Fixed |
| K-10 | 2026-10-13 | `SmartTorque.Tools.Simulator` is still the "Hello, World!" stub; implemented in P5.12. | Low | Fixed |
| K-11 | 2026-10-13 | EF warned that 5 byte-backed enum columns had DB defaults but no sentinel; the defaults were redundant with the C# property defaults, so they were dropped (migration `DropRedundantEnumDefaults`). | Low | Fixed |
| K-12 | 2026-10-13 | The BM preview's "headings present but zero data rows" empty state (`bm-preview-empty` in `Views/Bm/Preview.cshtml`) has neither a test nor a manual exercise: `bm-empty.xlsx` fails fatally at the worksheet level, so no committed fixture reaches that branch. Add a headings-only fixture when a real supplier sheet arrives (Q-03). | Low | Open |

| K-13 | 2026-10-14 | The unique `ProductCode` index had vanished from `AppDbContext` while migration `AddProducts`, the model snapshot and the live DB all still carry `IX_Products_ProductCode` (unique), so `MigrateAsync` threw `PendingModelChangesWarning` at app startup and `dotnet ef database update` failed; the old header's "2 known failures" were a separate problem (duplicate test-seed rows crashing `ConfirmAsync`'s `ToDictionaryAsync(p => p.ProductCode)`). **Fixed:** the index declaration was re-added (matches the P3.1 design record, the DB, and the uniqueness `ConfirmAsync` relies on — no migration needed), test seeds made idempotent, and `dotnet ef migrations has-pending-model-changes` reports none. | Medium | Fixed |
| K-14 | 2026-10-14 | `Memory.md` contains 151 `U+FFFD` replacement characters (156 before this session's rewrites) from an earlier encoding mishap — section signs (`§`), arrows (`→`), dashes and `N·m` were lost. Session content written since is clean UTF-8; historical lines were left as-is rather than guessed at. A repair pass (section refs after `Architecture`/`Design`/`PRD`/`Rules`, arrows in the legend) is safe when someone can verify the intent. | Low | Open |
| K-15 | 2026-10-16 | `dotnet format --verify-no-changes` failed (exit 2) with CS1998 in `BmImportService.cs:125` (async method without await). **Fixed same day** (commit 61e7c21): unnecessary `async` removed from `InspectHeadersAsync`, gate green. Row restored after being lost in merge a57a47d. | Low | Fixed |

---

## Environment Notes

Fill in as they become known.

| Item | Value |
|---|---|
| .NET SDK | 9.0.318 |
| SQL Server | `localhost` default instance (reachable from the dev box) |
| Dev OS / IDE | Windows / Antigravity IDE |
| Connection string | `Server=localhost;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True;` (in `appsettings.json` and user-secrets) |
| Seeded Admin | admin@smarttorque.local (email/password in user-secrets `af7b02d8-�`) |
| App URL (dev) | `http://127.0.0.1:5084` � run with `--launch-profile http` for Development |
| Simulator | enable with `Features:SimulatorEnabled=true` in Development |
| Upload root | `wwwroot/uploads` (`templates/`, `products/`) |
| sqlcmd tip | ODBC 18 needs `-C` to trust the local certificate: `sqlcmd -S localhost -d SmartTorqueDb -E -C` |
| Docker | Working. Docker Desktop 29.8.2, Compose v5.6.0; CLI is at `%LOCALAPPDATA%\Programs\DockerDesktop\resources\bin` and is NOT on the agent shell's inherited PATH - prepend it per command. `docker build -t smarttorque .` succeeds (543 MB) and the compose stack (web 8080, SQL 2022 on host 14330) comes up healthy and is HTTP-verified. Host network to `api.nuget.org` / `mcr.microsoft.com` / Docker Hub is slow and drops connections, and the engine once died during two concurrent big pulls - run pulls one at a time; base-image + NuGet caches make retries converge |

### Useful commands (fill in and keep current)
```
dotnet restore
dotnet build -warnaserror
dotnet test
dotnet format --verify-no-changes
dotnet ef migrations add <Name> -p src/SmartTorque.Data -s src/SmartTorque.Web
dotnet ef database update -p src/SmartTorque.Data -s src/SmartTorque.Web
dotnet run --project src/SmartTorque.Web --launch-profile http
dotnet run --project tools/SmartTorque.Tools.Simulator -- --server http://localhost:5084 --device ESP32-TW-001
```

---

## Hardware and Protocol Notes (fill in during Phase 0)

### Torque wrench
- Model:
- USB mode (HID / CDC / vendor):
- Sample output:
- How "stable/peak" is signalled:
- Unit and resolution:

### Vernier caliper
- Model:
- USB mode:
- Sample output:
- How "hold/data" is signalled:
- Unit and resolution:

### ESP32
- Board / USB host option:
- Firmware version:
- Transmitter IDs allocated:

---

1. Phase 8: Hardening:
   - P8.1: Calibration tracking and overdue blocking rules.
   - P8.2: Audit log interceptor for templates, products, devices, settings, users.
   - P8.3: Admin-approved manual-entry fallback workflow.
   - P8.4: Rework workflow with reason codes and admin approval.
   - P8.5: Security pass (headers, rate limits, upload checks, authorization audit).

---

## BM Sales Order & PO No Order Bifurcation (Completed)
- **Entities & Schema**:
  - `BmRow.SalesOrder` and `BmRow.PONo` (nvarchar(100)).
  - `Job.SalesOrder` and `Job.PONo` (nvarchar(100)).
  - Composite indexes `IX_BmRows_SalesOrder_PONo` and `IX_Jobs_SalesOrder_PONo`.
  - Migration `20261016093603_AddSalesOrderAndPoNo` applied to database.
- **Excel Importer**:
  - `BmImportService` detects and extracts `Sales Order` (aliases: `Sales Order`, `SalesOrder`, `SO No`, `SO Number`, `Sales Order No`, `SO`) and `PONo` (aliases: `PONo`, `PO No`, `PO Number`, `Purchase Order`, `Purchase Order No`, `PO`).
  - Preview displays `Sales Order` and `PO No` columns.
  - Column mapper allows custom override and auto-detects `Sales Order` and `PONo`.
- **Re-upload Refresh Matching**:
  - Matches open jobs on `(ProductCode, DueDate, SalesOrder, PONo)`. Different sales orders for the same item code maintain their own distinct jobs.
- **Workload Board Bifurcation (`/Bm/ByItem`)**:
  - Top level aggregates all jobs across all sales orders by `ItemCode`: Total to Manufacture, Total Completed, Total Remaining, progress bar.
  - Interactive collapsible bifurcation section (`Order Bifurcation (N)`) groups by `(SalesOrder, PONo)`.
  - Each order bifurcation displays: Sales Order No, PO No, Order Required Qty, Order Completed Qty, Order Remaining Qty, completion percentage, order fulfillment status badge, and the order's jobs list.
- **Verification**:
   - All 277 tests pass.
   - Zero warnings with `-warnaserror`.
   - Clean `dotnet format --verify-no-changes`.

### Backfilled entries (2026-10-19)

The merge `a57a47d` silently discarded two documentation commits (`8224278`, `61e7c21`) plus several feature commits never logged. The entries below are backfilled from the commit history; per-commit test counts were not re-derivable, so the joint status verified at HEAD on 2026-10-19 is quoted once here, not in every entry: **295/295 tests passing, build 0 warnings / 0 errors (-warnaserror), `dotnet format --verify-no-changes` exit 0.**

### Session (2026-10-16 — Tracker reconciliation after Phase 5) — restored from 8224278
- Goal: Reconcile the stale tracker with the code after P4.10/P4.11 landed.
- Done: Verified P4.10 (BM-6 re-upload refresh, 4 tests in `JobImportServiceTests`) and P4.11 (job detail + hold/resume/cancel state machine) against HEAD, ticked both; added K-15 (format gate CS1998).
- Decisions added: none (D-021/Q-04 still provisional-open).
- Note: Q-04 (BM re-upload row key) remains open; the refresh rule still matches on `(ProductCode, DueDate, SalesOrder, PONo)` since D-041.

### Session (2026-10-16 — Fix K-15: format gate CS1998) — restored from 61e7c21
- Goal: Green `dotnet format` gate.
- Done: Removed unnecessary `async` from `BmImportService.InspectHeadersAsync`; K-15 removed; gate exit 0.

### Session (2026-10-16 — Phase 7 complete: Reports and Dashboard) — 2746c1f
- Goal: P7.1–P7.8 traceability and operational reporting.
- Done: `ReportService` (indexed queries, shared filters); Session report + detail; Traceability (serial/JobNo/device); Pass/Fail summary with first-pass yield and per-step failure rate; Worker performance; Device usage; Excel (ClosedXML) + PDF (QuestPDF) exports matching on-screen filters; Admin dashboard KPIs; RPT-10 immutability asserted by tests (`ReportImmutabilityTests`, plus `AppDbContext.SaveChanges` override blocking result update/delete).
- Decisions added: none beyond Architecture §13 as written.

### Session (2026-10-16 — Per-unit tracking tokens + central API-key directory) — dff2e6d, 53d4361
- Goal: Physical-unit traceability and hardened key handling.
- Done: `JobUnit` entity (40-char `PRD-` tokens, unique indexes) with lazy creation; migration `AddJobUnitsAndApiKeyDirectory`; `ApiKeyDirectoryEntry`/`ApiKeyAccessGrant` with Main-Admin request/approve/revoke workflow (`/Admin/ApiKeys`); follow-up fix purged non-device keys so the directory holds hardware registration keys only.
- Decisions added: D-043, D-044.
- Note: worker-side unit UX landed later (fc10af7, D-046).

### Session (2026-10-17 — Operator defect workflow + unit switching) — fc10af7
- Goal: Owner-requested change to retry semantics; visible unit tokens everywhere.
- Done: Unlimited retries (`MaxRetries` no longer force-fails); new `ForceFail` ("Record Defect & Proceed", `OPERATOR_FORCE_FAIL`) in `SessionService.ProcessReadingAsync`; unit roster in `ExecutionStateDto` (`ExecutionUnitDto`/`UnitTokenNo`), `StartOrResumeSessionAsync(unitNo)`, `SwitchUnitAsync`, `GetJobUnitsAsync`; JobBoard unit picker modal + Execute unit switcher + "Submit Failed Bolt"; sessions/history report unit tokens; Session Detail gains a Defect Alert panel (failed last attempts, deviations, reason); reports carry `UnitNo`/`UnitTokenNo`.
- Decisions added: D-045, D-046. Q-11 raised to confirm the WRK-7 supersession.
- Problems: the replacement of retry-exhaustion semantics is not yet reflected in `PRD.md`/`Architecture.md` prose (docs kept on purpose; Memory is authoritative per Rules §1.9).

### Session (2026-10-19 — Assembly Management & Workload Assignment) — 9ad0380
- Goal: Connect products, assembly lines, workers and tasks; workload-based auto-allocation.
- Done:
  - Entities `AssemblyLine`, `ProductAssemblyMapping`; `ApplicationUser.CurrentAssemblyLineId`; `Job.AssignedAt`/`AssemblyLineId`/`EstimatedWorkload (decimal(18,2), default 1.0)`; migration `AddAssemblyManagementAndWorkloadAssignment` (indexes `(Jobs.AssemblyLineId, Status)`, unique `AssemblyCode`, unique `(ProductId, AssemblyLineId)`; SetNull FKs from Jobs/Users).
  - `AssemblyService` (~1,226 lines): lines CRUD + audit, product↔line mapping with IsDefault + pending-job backfill, worker↔line assignment, dashboard stats, `GetWorkerActiveWorkloadAsync` (Assigned+InProgress only), `AutoAssignJobsAsync` (least-loaded: workload → task count → last-assigned; strict line boundary; general pool for unmapped products; inactive line = skip; staffless line = jobs stay Pending + `AssemblyAlert` notification to Admin), `ManualReassignJobAsync` (boundary-enforced, explicit unassign on empty workerId).
  - `AssemblyController` (`/Assembly`, Admin): Index/Detail/Create/Edit/ToggleStatus/Delete/ProductMappings/AssignProduct/RemoveProduct/SetDefaultLine/Workers/AssignWorkerLine/AutoAssign/ReassignTask — views for all.
  - `_AdminLayout` gains the **Assembly** nav item (D-048); `_WorkerLayout` gains an Assembly Station chip; worker board shows station switcher (`SetAssemblyLine`), workload and line badges, and blocks starting cross-line jobs.
  - `JobImportService`: confirm stamps jobs with the product's default line; `AssignJobAsync` enforces the strict boundary + `AssignedAt`; auto-assign delegates to `AssemblyService`.
  - Product Builder auto-names components from the template (client + server fallback) — D-049.
- Decisions added: D-045 … D-049 (assembly rules), Q-11 open.
- Next step: Phase 8 P8.1 — calibration tracking (due dates, warnings on Execute/Device list, optional overdue block via setting); then backfill `Phases.md`-external decisions into `PRD.md`/`Architecture.md` only if the owner asks.

### Session (2026-10-19 — Settings page) — commit pending
- Goal: Owner requested the Settings page behind the existing (dead) `Settings` sidebar link; `ISettingsService` was an orphaned interface with no implementation, and 7 of 8 seeded setting keys were unread anywhere.
- Done:
  - `SettingsService` implements `ISettingsService` (+DI registration): typed `GetAsync<T>` (throws on corrupt values), `GetOverviewAsync`, `SetAsync`, and `SaveApplicationSettingsAsync` which validates ranges in consts (DeviceOfflineSeconds 5–600, ImageMaxMb 1–20, MaxRetriesDefault 1–10), upserts only **changed** keys, and writes one `AuditLog` row (`Setting`/`Updated`, old→new JSON batch) — `ApplicationSettingsSaveRequest` + `SettingOverview` records live in `SmartTorque.Core.Interfaces`.
  - Dead keys wired: `ImageMaxMb` → optional `maxBytes` param on `IImageUploadService.SaveAsync` (Template + Product `UploadImage` read the setting per call; invalid stored value surfaces as upload error); `MaxRetriesDefault` → `TemplateController.BuildEmptyStepsAsync` pre-fills wizard Stage-3 rows (clamped to 1–10); `DeviceOfflineSeconds` remains read inline by `DeviceController` (pattern untouched).
  - `SettingsController` (Admin, conventional routes): GET hydrates editable + read-only keys; POST parses raw strings (range/UI validation separate), on failure re-renders with admin input intact (Rules §7.7), TempData success/error, PRG.
  - `Views/Settings/Index.cshtml`: sectioned cards (Worker Devices, Templates & Image Uploads, BM Import, Planned Controls (Phase 8)) with status badges; 4 token-based `.badge-enforced/-deviceside/-planned/-reserved` classes added to `theme.css`; `/ColourMap` linked from the reserved tolerance row (D-032 entry-point stance kept).
  - Tests: `SettingsServiceTests` (20) + `SettingsControllerTests` (4) — ranges, header-map leniency, audit-row contents, no-write on invalid input, controller GET/POST happy + error paths.
- Decisions added: D-050.
- Tests run: 319/319 passing (295 pre-existing + 24 new). Build 0 warnings / 0 errors (`-warnaserror`). `dotnet format --verify-no-changes` exit 0 (auto-fixed final newlines in 5 new files, import order in the controller test).
- Problems / surprises: JSON with absent `BmHeaderMap` keys silently resolves to built-in defaults (STJ init-only defaults) — validation therefore enforces "valid object + no explicitly blank values" rather than requiring all six keys, matching the importer's own leniency.
- Next step: Await commit approval (working tree holds the 2026-10-19 backfill + this session's files). Then Phase 8 P8.1 — calibration tracking.

### Session (2026-10-27 — Product Builder: early duplicate-code guard + step-scoped alerts) — D-051
- Goal: Two owner-reported UX bugs in the Product Builder: (1) entering a duplicate `ProductCode` was only detected at the final Step-3 form POST (after templates had already been created); (2) the Step-1 "Please specify Product Code and Product Name before continuing." banner kept hanging over Step 2 after a successful fix, until manually dismissed.
- Done:
  - New AJAX endpoint `POST /Product/CheckCode` (`[HttpPost][IgnoreAntiforgeryToken]`, returns `{ available }` from `ProductService.IsCodeUniqueAsync`; empty/whitespace code → 400). Request DTO `CheckCodeRequest { Code, ExcludeProductId }` in `ProductViewModels.cs`.
  - `Builder.cshtml`: inline `#product-code-feedback` div under the Product Code field.
  - `product-builder.js` Step-1 guard refactor:
    - `goToStep` validates (code+name), then delegates to `verifyCodeThenContinue` — the duplicate check must pass before any Step-1 → 2/3 transition (`applyStep` extracted from the old `goToStep` body).
    - `onCodeEdited`: live availability feedback — debounced 400 ms on `input`, immediate on `blur`, with `text-success`/`text-danger` inline status under the field.
    - `formSubmitGuard`: final POST re-runs the check first; duplicate → back to Step 1 with the error; endpoint failure falls back to `form.submit()` so the server-side check still decides.
    - Token-based race guard (`codeCheckToken`) cancels stale in-flight checks everywhere; edit mode excludes the edited product via the hidden `EditingProductId` input.
  - Step-scoped alerts: `showAlert(message, isError, ownerStep?)` + `hideAlert()`; `applyStep` hides banners owned by a different step; the Step-1 validation alert is tagged `ownerStep=1` and also cleared as soon as its conditions no longer apply. Untagged banners keep legacy behaviour. (Second owner bug.)
  - Tests: `ProductControllerTests` gained shared `BuildProductController`/`AddProductAsync` helpers + 4 `CheckCode` tests (taken → `available=false` case-insensitive, available → true, own code excluded in edit mode → true, empty code → 400). 330 → 334 total.
- Decisions added: D-051, D-052.
- Tests run: `dotnet build SmartTorque.sln -warnaserror` clean; 334/334 passing; `dotnet format --verify-no-changes` exit 0.
- Follow-up (same day, second owner request — product image picker):
  - Picking a product image now opens the shared crop editor in "product" mode (`openCropModal(src, target)` with a `cropTarget` switch; modal title subject spans Product/Component). "Crop & Use Image" uploads the cropped JPEG (canvas → `image/jpeg` blob) and the new "Use Without Cropping" footer button uploads the untouched original — both to `/Product/UploadImage` (same endpoint, form token appended).
  - Step-1 preview is now a 72px thumbnail with an eye **View** button opening the new `#image-view-modal` (large view) plus the existing **Remove Image** button, which also clears the file input; the standalone "Upload" button and its handler were removed (upload happens from the crop modal).
  - Viewer refinement (third owner request, same day): `#image-view-modal` became a **fit-to-size zoomable viewer** — fixed-height dark viewport (`height:80vh`, `overflow:hidden`, no scrollbars) with the image object-fitted; mouse wheel zoom (1×–6×, around the cursor), two-finger pinch zoom (around the pinch midpoint), drag-to-pan when zoomed, double-click toggles fit ↔ 2×, a zoom % badge in the header, a hint line, and reset-to-fit on open/full zoom-out (`resetViewZoom`/`zoomViewAt`/pointer+touch handlers in `product-builder.js`).
  - The Step-2 component-image crop/marker flow is untouched (default "gen" target); `refreshStep3Summary`/summary still read `#image-preview`/`#image-path`, which kept their element ids.
- Problems / surprises:
  - The owner's running `SmartTorque.Web` (PID 20360) locked `bin\Debug\net9.0\SmartTorque.Web.dll/.exe` and, being pre-fix, would have served stale code anyway — it was stopped for the rebuild and restarted from `bin\Debug\net9.0` with `ASPNETCORE_ENVIRONMENT=Development` + `ASPNETCORE_URLS=http://localhost:5084` (PS 5.1 `Start-Process` has no `-Environment`; used `ProcessStartInfo.EnvironmentVariables`). `/health` verified 200 Healthy after restart.
  - The `-warnaserror` build exposed a **pre-existing** CS8629 in `JobImportServiceTests` (`job.SourceBmRowId.Value`) — minimally fixed with `!.Value` (file otherwise untouched; warning existed committed, only surfaced now).
  - `dotnet format --verify-no-changes` flagged missing/extra final newlines on three committed-but-untouched files (`JobImportService.cs`, `DevSimulatorController.cs`, `TemplateController.cs`) plus the two edited test files; `dotnet format` normalised all, gate re-verified exit 0.
  - `.editorconfig` says `insert_final_newline = false` — the format gate's FINALNEWLINE notices **remove** a trailing newline; earlier fix ("adding final newlines") was wrong-headed and files now pass WITHOUT one. Hand-off: files created with LF-only endings must be converted to CRLF (`git` shows them as modified anyway).
- Follow-up (same day, fourth owner request — multi-image gallery) — D-053:
  - `Product.ImagePath` kept as the primary-store mirror; new `ProductImage` table (backfilled from existing ImagePath rows in migration `AddProductImages`) stores the ordered gallery; `SaveProductRequest` gained `Images`, the service normalises (trim/dedupe-case-insensitive/`uploads/products/`-prefix-only anti-path-traversal), sets `ImagePath = gallery[0]`, replaces rows on update, clones the gallery, and falls back to the legacy single path when no gallery is posted.
  - Builder: `image-file` input is `multiple`; files queue through the shared crop editor one at a time ("Crop & Use Image" or "Use Without Cropping" per file); closing the modal without using a file abandons the rest of the batch; the queue advances only after both modal close and upload settle. Each accepted upload is a chip (64px thumb + eye → the zoomable viewer + trash → remove) whose hidden inputs use the non-sequential `Images.Index` binder sentinel; removing renumbers automatically (`productImageSeq`). First gallery image mirrors into `#image-path` and the Step-3 summary preview. Edit mode seeds chips from server-rendered hidden inputs.
  - Detail page / lists still show only the primary (first) image — owner may later want a full gallery display.
  - Tests: +6 (gallery create/edit via controller, gallery persists/mirror primary, legacy ImagePath fallback, foreign-path/dup/blank sanitisation, gallery replace on update) — 334 → 340 total.
- Follow-up (same day, fifth owner request — template generator markers) — D-054:
  - Fixed the "pin spawns somewhere else" bug: the placement canvas `#gen-image-container` letterboxes the centered `#gen-component-image` (`mx-auto`, `max-height:280px`) but pins were positioned with container-relative % — replaced by `#gen-marker-layer`, an overlay whose box mirrors the img rect (re-synced on load/resize) that hosts all pins; the similarity modal's pin layers (plain static divs resolving % against a padded `position-relative` wrap) got the same geometry fix, re-rendered when their images finish loading.
  - Pins are now **draggable** (pointer events with capture; dragging writes the row's hidden `MarkerX/Y` live; clamped 0–100 of the image box; `touch-action:none` for touch; grab/grabbing cursors; tap = select only — propagation stopped so a tap doesn't re-place the same row + auto-advance). `selectGenStep` gained a `skipMarkerRender` variant used at `pointerdown` — a full render there would destroy the grabbed dot and drop its pointer capture.
  - Marker size: **range slider** (14–42 px, live px value, localStorage-persisted) beside the existing S/M/L presets; presets snap the slider; slider position never desyncs (`syncMarkerSizeControls` at init + on every change).
- Follow-up (same day, sixth owner request — per-marker size) — D-055:
  - Each marker now has its **own persisted size**. `TemplateStep.MarkerSize int?` (migration `AddTemplateStepMarkerSize` on AppDbContext) flows through `StepRequest` (+`MapSteps` clamp 14–56), `QuickStepDto`, `StepRowViewModel`/`StepDetailViewModel`, wizard Stage3 round-trip (hidden `Steps[i].MarkerSize`), similarity-check projections (`markerSize` for both existing + new spec), `ExecutionStepDto` (worker), and the device SSE payload (`markerSize`).
  - Builder Step 2 (generator): rows carry a hidden `.step-marker-size`; the slider/S-M-L resize **the selected row's marker individually** when it has one (stamped at placement time, cleared when the marker is cleared), otherwise they set the default for future placements; `selectGenStep` syncs the controls to the selection; `collectGenSteps` posts `markerSize`; similarity modal dots use each step's size (fallback 18); template Detail preview pins render at their stored size.
  - Worker Execute + template Detail pin "size" toolbars became **scale tools**: every pin's own `data-base-size` is scaled from localStorage (28 px = 100 % on Execute, 24 px on Detail); labels show percent.
  - Wizard Stage3 got the same per-row semantics (`placeMarker` stamps the current default, `addMarkerDot` reads the row's own size, `setMarkerSize` re-sizes only the selected placed marker).
- Follow-up (same day, seventh owner request — marking UX) — D-056:
  - Owner: the Step-2 inline image is "visibly small" so marking is hard. Added the **large marker popup**: opens from each row's pin button or the canvas' "Large View" button; image fills ~`min(96vw,1250px)` × 78vh; same drag/click-place/auto-advance/per-size semantics as the inline panel with mirrored-overlay geometry; modal size tools and the inline ones share `setMarkerSize` (selection-aware, both slider/preset groups sync). Inline image also enlarged to 340 px. Gates green (341/341, build clean, format 0); user's `SmartTorque.Web` stopped twice for rebuilds (PIDs 2512, 5828).
- Follow-up (same day, eighth owner request — placement UX) — D-057:
  - The Step-2 upload card preview was also tiny, so it no longer renders the picture: a compact status row with a **View (eye)** button opens the shared zoomable viewer (`#image-view-modal`, title "Image Preview"); upload/remove manage the row; the marker canvas keeps its image (placement needs it) and its Large View button. Gates green (341/341, build 0/0, format 0); user's rebuilt instance stopped (PID 4000).
- Follow-up (same day, owner-reported popup bug) — D-058:
  - In the large popup, tapping a marker then reaching for the size slider found the wrong step selected: the `pointerup` rebuild detached the tapped dot so its follow-up `click` fell through to the canvas (place + auto-advance = selection stolen). Suppressed that single click after rebuilds on both surfaces; `setMarkerSize` now re-renders the popup layer too so per-marker resizing is actually visible there; popup clicks near an existing marker select it instead of placing. Gates green (341/341, build 0/0, format 0); user instance stopped (PID 23332).
- Follow-up (same day, ninth owner request — Step-3 summary image popup) — D-059:
  - The Product Summary card thumbnail in Step 3 is now expandable: a dark eye overlay button (top-right, `#btn-summary-view-image`) and clicking the thumbnail itself open the shared zoomable `#image-view-modal` with the summary image (cache-busted, `resetViewZoom`); `refreshStep3Summary` shows/hides the eye with the image so gallery + placeholder states stay in sync. Gates green (341/341, build 0/0, format 0); user instance stopped (PID 21084).
- Follow-up (same day, tenth owner request — compulsory markers) — D-060:
  - Template generation and Step 2 → 3 navigation now demand a placed pin (X+Y) on every bolt step: first attempt bounces to Step 2, selects + scrolls to the first unmarked row, alerts with the step's name, and flags all unmarked rows red (`table-danger`) until they are placed (live refresh from `updateRowMarker` and the remove-image clean-up). Init seeding of the 4 draft rows now only happens when the product has no attached templates, so edit mode with components navigates freely; deleting all draft rows is the escape hatch for "no template" drafts. Gates green (341/341, build 0/0, format 0, `node --check` clean); user instance stopped (PID 10840).
- Follow-up (same day, cosmetic) — double plus icon:
  - The Step-3 "Generate / Attach Another Template" button rendered "＋ +": `bi-plus-lg` icon followed by a literal `+` in the label text — dropped the literal. Gates green (341/341, build 0/0, format 0); user instance stopped (PID 5108).
- Follow-up (same day, UI removal) — Auto-Allocate button off the Assembly Management toolbar:
  - Owner asked to remove the "Auto-Allocate Workload" button from the Assembly Management index toolbar (header anti-forgery form deleted); the `AssemblyController.AutoAssign` endpoint stays because the Assembly Detail and BM Workload pages still use it. Gates green (341/341, build 0/0, format 0); user instance stopped (PID 25192).
- Tests run: `dotnet build SmartTorque.sln -warnaserror` clean; **341/341 passing** (+1 `CreateInlineTemplate_ClampsOutOfRangeMarkerSize`; existing inline-template test now asserts 16/null sizes); `dotnet format --verify-no-changes` exit 0 (new migration file needed trailing-newline trim).
- Next step: Phase 8 P8.1 — calibration tracking (due dates, warnings on Execute/Device list, optional overdue block via setting).
