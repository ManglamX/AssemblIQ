# SmartTorque

Guidance and traceability system for torque-controlled assembly: template design, product
configuration, BM (Bill of Materials) workbook import, job assignment, and work guidance.

ASP.NET Core MVC on **.NET 9**, EF Core 9 + **SQL Server**, ASP.NET Core Identity, Serilog,
ClosedXML (Excel import), QuestPDF (reports).

- Solution file: `SmartTorque.sln`
- Web app: `src/SmartTorque.Web`
- Specs and design docs live one level up in `..` (`PRD.md`, `Design.md`, `Architecture.md`, `Phases.md`).

---

## 1. Prerequisites

| Requirement | Notes |
|---|---|
| .NET SDK 9.0 | 9.0.318 verified. `dotnet --version` |
| SQL Server | Any edition (Developer/Express/LocalDB) with Windows auth. Default instance `localhost`. |
| `dotnet-ef` CLI | EF Core 9.0.13. Needed for manual migration commands. |
| Docker Desktop | Optional. For the containerised stack (Linux containers). |

```powershell
dotnet --version                 # expect 9.0.x
sqlcmd -S localhost -E -C -Q "SELECT @@VERSION"     # -C trusts the local certificate (ODBC 18)
dotnet tool list -g | Select-String ef              # expect dotnet-ef 9.0.13
# if missing:
dotnet tool install --global dotnet-ef --version 9.0.13
docker --version                 # optional
```

---

## 2. Repository layout

```
SmartTorque/
├─ SmartTorque.sln
├─ Directory.Build.props          # Nullable, LangVersion 12, warnings-as-errors (CS86xx)
├─ .editorconfig                  # code style + C# files have no trailing newline
├─ Dockerfile / .dockerignore / docker-compose.yml
├─ src/
│  ├─ SmartTorque.Core/           # entities + enums
│  ├─ SmartTorque.Data/           # AppDbContext, Migrations/, DataSeeder
│  ├─ SmartTorque.Services/       # business services (import, jobs, images, PDF)
│  └─ SmartTorque.Web/            # MVC app: Areas/ (Admin, Worker), Controllers, Views, wwwroot
├─ tests/SmartTorque.Tests/       # 171 tests
└─ tools/SmartTorque.Tools.Simulator/   # placeholder console tool
```

Runtime data (not in source control):

- Uploads: `src/SmartTorque.Web/wwwroot/uploads/{templates,products,bm}`
- Logs: `src/SmartTorque.Web/logs/smarttorque-*.log`

> The Web project contains a `Models/` folder of EF-scaffolded classes kept **for reference only**.
> The application uses `SmartTorque.Data.AppDbContext` + `SmartTorque.Core.Entities`. Because two
> context types are discoverable, every `dotnet ef` command below must pass `--context AppDbContext`.

---

## 3. Configure secrets (development)

The app reads its connection string and the first admin's credentials from configuration.
For local development, store them in user-secrets:

```powershell
cd src\SmartTorque.Web

dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True"
dotnet user-secrets set "Seed:AdminEmail" "admin@smarttorque.local"
dotnet user-secrets set "Seed:AdminPassword" "Admin@12345"

dotnet user-secrets list
```

Notes:

- `appsettings.json` already contains the same connection string, so `ConnectionStrings:Default`
  is only needed if your server differs from `localhost`.
- Roles, settings, templates, sample products and the colour map are seeded regardless. The admin
  user is **skipped with a warning** when `Seed:AdminEmail` / `Seed:AdminPassword` are absent.
- User-secrets load only when `ASPNETCORE_ENVIRONMENT=Development`. In other environments use
  environment variables instead: `ConnectionStrings__Default`, `Seed__AdminEmail`,
  `Seed__AdminPassword`.

---

## 4. Database setup

### Option A - automatic (recommended)

Nothing to do beyond having SQL Server reachable. On startup the app applies all EF migrations and
runs the seeder (`Program.cs` -> `RunMigrationsAndSeedAsync`):

- creates the `SmartTorqueDb` database if it does not exist,
- applies the 5 migrations (InitialSchema, AddTemplatesAndAuditLog, AddProducts, AddBmAndJobs,
  DropRedundantEnumDefaults),
- seeds roles (`Admin`, `Worker`), the admin user, default settings, templates, sample products and
  the colour map.

```
[INF] Database migrations applied
[INF] Database seeding complete
```

If the database is unreachable, the app logs the error and exits - fix connectivity and restart.

### Option B - manual

Create the database yourself and then run the migrations:

```powershell
# 1. create the database (skip if it already exists)
sqlcmd -S localhost -E -C -Q "IF DB_ID('SmartTorqueDb') IS NULL CREATE DATABASE SmartTorqueDb;"

# 2. apply migrations (--context AppDbContext is required - two contexts are discoverable)
dotnet ef database update `
  --project src/SmartTorque.Data `
  --startup-project src/SmartTorque.Web `
  --context AppDbContext

# 3. verify
sqlcmd -S localhost -d SmartTorqueDb -E -C -Q "SELECT COUNT(*) AS TableCount FROM sys.tables;"
```

### Migration maintenance

```powershell
# list migrations
dotnet ef migrations list --project src/SmartTorque.Data --startup-project src/SmartTorque.Web --context AppDbContext

# add a migration after changing entities
dotnet ef migrations add <Name> --project src/SmartTorque.Data --startup-project src/SmartTorque.Web --context AppDbContext

# check the model has no un-migrated changes
dotnet ef migrations has-pending-model-changes --project src/SmartTorque.Data --startup-project src/SmartTorque.Web --context AppDbContext
```

### Reset the database

```powershell
sqlcmd -S localhost -E -C -Q "ALTER DATABASE SmartTorqueDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE SmartTorqueDb;"
# then start the app again (Option A) or run the manual migration command (Option B)
```

---

## 5. Build, test and run

```powershell
# from the repository root (SmartTorque/)
dotnet restore SmartTorque.sln
dotnet build   SmartTorque.sln -v q --nologo
dotnet test    SmartTorque.sln --nologo -v q            # 171 tests
dotnet format  SmartTorque.sln --verify-no-changes -v q  # style gate (exit 0)
```

Run the web app:

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SmartTorque.Web --no-launch-profile --urls "http://127.0.0.1:5084"
```

Then open <http://127.0.0.1:5084/Account/Login> and sign in:

| User | Credentials |
|---|---|
| Admin | `admin@smarttorque.local` / `Admin@12345` (the values you set in user-secrets) |

The existing local admin credential is `admin@smarttorque.local` / `Admin@12345`.

### Main pages

| URL | Purpose |
|---|---|
| `/` | Landing page (redirects anonymous users to login) |
| `/Account/Login`, `/Account/Logout` | Sign in / out |
| `/Admin/Dashboard` | Admin area dashboard (post-login landing for Admin) |
| `/Worker/JobBoard` | Worker area job board |
| `/Template` | Torque templates: list, 3-stage create, detail, activate |
| `/Product` | Products: list/search, builder, components, images, clone, activate |
| `/ColourMap` | Colour map entries: list, create, edit, activate |
| `/Bm` | BM workbook upload + import history |
| `/Bm/Preview/{id}` | Review imported rows, set priority/skip, confirm or cancel |
| `/Bm/Workload` | Workload board: filter, sort, assignment entry point |
| `/health` | Health check (includes a database probe) |

To exercise the BM import flow you can upload `tests/SmartTorque.Tests/Fixtures/bm-default-headers.xlsx`.

---

## 6. Docker (optional)

Containerised stack: web app + SQL Server 2022. The app image is built from `Dockerfile`;
`docker-compose.yml` wires both services with a health-gated startup.

```powershell
docker compose up -d --build
docker compose ps                 # both services should become healthy
docker compose logs -f web        # watch migrations + seeding

# App: http://localhost:8080   admin@smarttorque.local / Admin@12345
# SQL: localhost,14330         sa / SmartTorque_SA123
```

Lifecycle:

```powershell
docker compose down        # stop, keep the database volume
docker compose down -v     # stop and wipe the database
docker compose up -d --build   # rebuild after code changes
```

Override defaults with a `.env` file next to `docker-compose.yml` (or shell env):

```
MSSQL_SA_PASSWORD=<strong password>
SEED_ADMIN_EMAIL=admin@smarttorque.local
SEED_ADMIN_PASSWORD=<admin password>
```

Things to know:

- Integrated Security cannot work inside a container, so the stack uses SQL authentication
  (`sa`). The connection string is supplied via `ConnectionStrings__Default`.
- The compose database is a **fresh, separate** database inside the `mssql-data` volume - it does
  not see a database running on the host.
- The image runs as a non-root user; `logs/` and `wwwroot/uploads/` are volumes so data survives
  container replacement.
- `/health` includes the EF `DbContext` check, so the container reports unhealthy when SQL is down.
- The first `docker build` needs a reliable connection to `mcr.microsoft.com` (base images) and
  `api.nuget.org` (NuGet restore). On a flaky link the restore step can fail with
  `unexpected EOF` / timeouts - the base-image layers stay cached, so simply retry:

  ```powershell
  docker build -t smarttorque .      # rerun; cached layers are reused
  ```

---

## 7. Troubleshooting

| Symptom | Cause / fix |
|---|---|
| App exits right after start, `Error during database migration/seeding` | SQL Server unreachable or connection string wrong. Test with `sqlcmd -S localhost -E -C`. |
| `A network-related or instance-specific error` | SQL Server not running, or use the right instance name (`localhost\SQLEXPRESS`). |
| `Cannot open database "SmartTorqueDb"` | Permissions: the Windows account needs `db_owner` (or create the DB yourself - Option B). |
| `More than one DbContext was found` from `dotnet ef` | Always pass `--context AppDbContext` (plus `--project`/`--startup-project`). |
| No admin user is created | `Seed:AdminEmail` / `Seed:AdminPassword` missing (check `dotnet user-secrets list`; secrets only load in Development). |
| `Failed to determine the https port for redirect` warning | Harmless when running HTTP-only. Configure an HTTPS endpoint or ignore. |
| Login page loads but `/Report` returns 404 | Reports are Phase 7 and not implemented yet. |
| Port already in use | Change `--urls` (e.g. `http://127.0.0.1:5085`) or stop the other instance. |
| `dotnet format` reports changes | Run `dotnet format SmartTorque.sln` and review. C# files intentionally have no final newline (`.editorconfig`). |
| Docker build fails during `dotnet restore` | Unstable network to nuget.org. Retry; completed layers are cached. |

---

## 8. Quality gates

Before considering any change complete:

```powershell
dotnet build  SmartTorque.sln -v q --nologo            # 0 warnings, 0 errors
dotnet test   SmartTorque.sln --nologo -v q            # all tests green (171)
dotnet format SmartTorque.sln --verify-no-changes -v q # exit 0
```
