# Rules — Smart Torque Guidance & Traceability System

These rules bind every developer and every AI coding agent working on this project. They are short on purpose: follow them literally. If a rule blocks the work, stop and ask the owner; do not work around it.

Read order for a new session: `Memory.md` → `Rules.md` → `PRD.md` → `Architecture.md` → `Phases.md` → `Design.md` (before any UI work).

---

## 1. How an AI Agent Must Work

1. **Read `Memory.md` first.** It says which phase and task is current and what decisions are already made.
2. **Work on one task at a time**, the next unchecked task in `Phases.md`. Do not jump ahead to later phases.
3. **Do not invent requirements.** If the PRD or Architecture does not answer something, pick the simplest option that fits the rules, record it in `Memory.md` under *Decisions*, and keep going. If the choice is expensive to reverse (schema, public contract, security), stop and ask the owner.
4. **Do not expand scope.** Out of scope: PLC integration, ERP integration, native mobile apps, multi-site, Supervisor role. Only the `ExecutionMode` field is allowed as a PLC hook.
5. **Small, reviewable changes.** One task, one commit (or one small PR). Each change builds and passes tests before moving on.
6. **Prove it works.** Build, run tests, and run the app for UI changes. Report what you ran. Never claim "done" on unbuilt or untested code.
7. **Update `Memory.md` at the end of every session**: what was done, what is next, new decisions, open questions, known issues.
8. **Never delete or rewrite history data or migrations that were already applied.** Add new migrations instead.
9. **If something in these docs conflicts**, precedence is: `Rules.md` > `Architecture.md` > `PRD.md` > `Phases.md` > `Design.md`. Record the conflict in `Memory.md`.

---

## 2. Technology Rules

| Area | Rule |
|---|---|
| Language/runtime | C# 12, .NET 8 (or 9). Nullable reference types **enabled**. Warnings are errors for nullable and obsolete. |
| Web | ASP.NET Core **MVC with Razor Views**. No Blazor, no SPA framework. Small vanilla JS modules for the live screen. |
| Data | EF Core + MS SQL Server, **code-first migrations**. |
| Auth | ASP.NET Core Identity, cookie auth, roles `Admin` and `Worker` only. |
| Real-time | SignalR. |
| UI framework | Bootstrap 5 **vendored locally**, themed by `theme.css`. No CDN links, no external fonts at runtime (the plant runs offline). |
| Excel | ClosedXML. |
| PDF | QuestPDF. |
| Logging | Serilog. |
| Tests | xUnit + FluentAssertions. |
| New packages | Add one only when it removes real work; record it in `Memory.md`. No packages with restrictive or unclear licences. |

---

## 3. Architecture Rules

1. **Respect the layers** (see `Architecture.md` section 3):
   - `Core` references nothing.
   - `Services` references `Core` only.
   - `Data` references `Core` only.
   - `Web` wires everything.
2. **Controllers are thin.** Bind → validate → authorize → call a service → return a result. No business rules, no `DbContext`, no LINQ over entities in controllers.
3. **Business logic lives only in `SmartTorque.Services`.** Tolerance checks, priority/colour mapping, BM import, job assignment, session logic, reading acceptance.
4. **Views get ViewModels, never entities.** Map explicitly (a small mapper or `Select`).
5. **One app, both instruments.** No separate controllers, views, tables or code paths per instrument. Differences live in `IInstrumentHandler` implementations and in data (`InstrumentType` on the step). Never write `if (instrument == Torque) … else …` in a controller or view; use the handler.
6. **The instrument is chosen by the template step.** The worker never chooses it, and no screen offers a manual instrument toggle.
7. **Dependency injection** for all services; no static state, no service locators. Inject `TimeProvider`/`IClock` instead of calling `DateTime.UtcNow` in services.
8. **No logic in `Program.cs` beyond composition.** Group registrations in extension methods.

---

## 4. Data Rules

1. **Money-grade precision for measurements:** `decimal` in C#, `DECIMAL(10,3)` in SQL. Never `float`/`double` for target, tolerance or actual values.
2. **UTC everywhere in storage.** Convert to local time only when displaying.
3. **Session results are append-only.** The application code exposes no update or delete for `SessionResults`. Corrections are new attempts or rework records with a reason code.
4. **Snapshot rule:** a `SessionResult` copies target, tolerance, tolerance mode, and computed lower/upper limits at the moment of capture. Reports read the snapshot, never the live template.
5. **Template versioning:** a template that has been used is never edited in place; saving creates a new version (see `Architecture.md` section 11). Historic sessions keep their version.
6. **Never hard-delete** templates, products, devices, jobs, sessions, results. Use `IsActive` / status. Admins can deactivate.
7. **Migrations:** every schema change is an EF migration with a descriptive name. Never edit an applied migration. Do not auto-migrate in production.
8. **Transactions** for every multi-row write: BM confirm, reading accept, session complete, template version save.
9. **Concurrency:** `RowVersion` on `Jobs`; handle `DbUpdateConcurrencyException` with a friendly "already taken" message.
10. **Seed data** (roles, colour map, default settings) is idempotent and in code, not in SQL scripts pasted by hand.
11. **Indexes** from `Architecture.md` section 5.4 are created in migrations.
12. **Identifiers:** table names plural PascalCase; columns PascalCase; enums stored as `TINYINT`.

---

## 5. Security Rules

1. Every controller action is `[Authorize]` by default (fallback policy). Anonymous only for login and static files.
2. Role checks via `[Authorize(Roles = "Admin")]` / `[Authorize(Roles = "Worker")]`. A worker may access only their own sessions and jobs (check ownership server-side).
3. All form POSTs use anti-forgery tokens (global auto-validate). The device API uses API keys instead.
4. **Device API keys:** generated randomly, shown once, stored hashed, sent in `X-Api-Key`, never logged, never returned after creation. Reject unregistered or inactive devices. Rate limit `/api/device/*`.
5. **No secrets in source control.** No connection strings with passwords, no default admin password in code. Use user-secrets locally and environment variables or protected config in deployment. The README explains how to seed the first Admin.
6. **Validate on the server** even if the client validates. Validate file uploads by extension, magic bytes and size; store under generated GUID names.
7. **EF parameterised queries only.** No string-concatenated SQL.
8. **HTML-encode by default** (Razor does); never use `Html.Raw` on user data.
9. Sessions and cookies: HttpOnly, Secure when HTTPS, SameSite=Lax, sliding expiry, lockout on failures.
10. Never log passwords, API keys or tokens.

---

## 6. Coding Standards

1. **Naming:** `PascalCase` for types/methods/properties, `camelCase` for locals/parameters, `_camelCase` for private fields, `I` prefix for interfaces, `Async` suffix on async methods. Enums singular (`JobStatus`). Controllers end with `Controller`; view models end with `ViewModel`; DTOs end with `Dto`.
2. **Async all the way** for I/O. Accept `CancellationToken` in service and repository methods that hit the database.
3. **No magic numbers or strings.** Statuses, priorities and instrument types are enums. Settings come from `Settings`/options classes.
4. **Small methods, single responsibility.** A method over ~40 lines or a class over ~300 lines needs a reason.
5. **Domain errors are exceptions or result types**, not null returns. Use `Result<T>` or typed exceptions (`DomainException`) and map them in controllers to model errors/messages.
6. **Comments explain why, not what.** XML docs on public service interfaces.
7. **No dead code, no commented-out code, no TODOs without an item in `Memory.md`.**
8. **Formatting:** `.editorconfig` committed; `dotnet format` clean before commit.
9. **Razor:** use tag helpers and partials; no inline `<style>` blocks; JS in `wwwroot/js` (one module per screen).
10. **Magic of the stack, not custom frameworks.** Prefer built-in ASP.NET Core features over writing infrastructure.

---

## 7. UI Rules (see `Design.md` for full detail)

1. **Theme:** white `#FFFFFF` and purple-magenta `#BD1362`. Use only CSS variables from `theme.css`. **No hex colour literals in views or JS**; one place defines colours.
2. **Never use colour alone.** PASS/FAIL show an icon **and** a text label. Priority shows as a labelled badge (`BLACK`, `RED`, `YELLOW`, `GREEN`, `WHITE`) so the brand colour is never mistaken for a priority.
3. **Worker screens:** large touch targets (≥ 48 px), high-contrast live value, minimal clicks, no required instrument selection.
4. **Responsive** Admin and Worker layouts (tablet first for Worker).
5. **No CDN assets.** Vendor everything; the app works offline.
6. **Accessibility:** labelled form fields, visible focus, WCAG AA contrast, keyboard operable.
7. Show server validation errors inline next to the field; never lose user input on a failed POST.

---

## 8. Device and Real-Time Rules

1. **Server decides PASS/FAIL**, never the device and never the browser. Live "in tolerance" colouring in the browser is only a hint.
2. The device is trusted only through its API key; the transmitter ID in the payload must match the key's device.
3. A reading is persisted only when `isStable = true`, an in-progress session expects that device, the step's instrument matches the device's instrument type, and it is not a duplicate (`seq`).
4. Process one stable reading per session at a time (lock per session).
5. Out-of-order steps are rejected; steps follow `SequenceNo`.
6. Live (non-stable) readings are broadcast, **never stored**.
7. Offline buffering is the device's job; the server must be idempotent (`DeviceId + seq`).
8. Manual entry is a flagged exception (`IsManualEntry = true`, approver recorded) and is disabled unless `ManualEntryFallback` is on.
9. **Build against the simulator first.** Do not block on hardware. The simulator page is available only when `Features:SimulatorEnabled = true` (Development).
10. Units are fixed (N·m, mm). The firmware converts before sending; the server rejects other units.

---

## 9. Testing Rules (the gate for "done")

A task is done only when **all** are true:

1. The solution **builds with zero warnings** treated as errors where configured.
2. **Unit tests** exist for new service logic and pass. Mandatory test targets:
   - Tolerance evaluator: exact lower bound, exact upper bound, just outside, zero tolerance, percent mode, rounding.
   - Priority mapping and ordering (Black first, then due date, then upload time).
   - BM parser: RGB fill, theme colour with tint, no fill, pure black, unknown colour, bad quantity/date, unknown product code.
   - Template versioning (edit unused vs used).
   - Reading acceptance (stable/unstable, wrong instrument, no session, duplicate, retry exhaustion, last step).
   - Job state transitions and the "job already taken" concurrency case.
3. **Integration tests** for new endpoints and role authorization (Admin-only pages return 403/redirect for Worker and vice versa).
4. For UI work: run the app, exercise the screen, and check it at tablet width.
5. No new analyzer warnings; `dotnet format --verify-no-changes` is clean.
6. `Memory.md` is updated.

---

## 10. Git and Workflow Rules

1. Branch per phase or task: `phase-2/template-wizard`. Commit messages: imperative, with the requirement ID, e.g. `TPL-8: create new template version when template is in use`.
2. Never commit `bin/`, `obj/`, user secrets, uploads, `.env`, or `*.mdf`.
3. Never force-push shared branches.
4. Do not reformat unrelated files in a feature commit.
5. Keep the README current: prerequisites, how to run, how to seed the Admin, how to run the simulator, how to run tests.

---

## 11. Things You Must Not Do

- Do not put business logic in controllers, views, hubs, or JavaScript.
- Do not create a separate app, project, controller set or table set for the Vernier caliper.
- Do not add a manual instrument selector for workers.
- Do not store measurements as `float`/`double`.
- Do not edit or delete session results.
- Do not edit a used template in place.
- Do not hard-code colours, passwords, API keys, connection strings or URLs.
- Do not use a CDN or any online dependency at runtime.
- Do not depend on real hardware to finish a phase.
- Do not skip tests to save time.
- Do not add PLC, ERP or mobile features.
- Do not "fix" a failing test by weakening it without writing the reason in `Memory.md`.
- Do not use an entity as a view model or expose entities from API endpoints.
- Do not auto-apply migrations in production.

---

## 12. When to Stop and Ask the Owner

- The real BM sheet layout or colours differ from what `PRD.md` describes.
- The instruments' USB protocol is known and changes the reading/accept rule.
- A schema change would alter stored traceability data.
- A requirement seems to conflict with another.
- Adding a package with unclear licensing.
- Anything involving real plant data, credentials, or deployment access.

Record the question in `Memory.md` under *Open Questions* with the date and what is blocked.
