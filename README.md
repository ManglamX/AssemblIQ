# SmartTorque - Setup Guide

ASP.NET Core MVC application (.NET 9) with SQL Server. These steps assume Windows and a local
SQL Server installation.

## Before you start

Install these:

1. **.NET 9 SDK** - https://dotnet.microsoft.com/download/dotnet/9.0
2. **SQL Server** (Developer or Express edition) with Windows authentication
3. **EF Core command line tool**

```powershell
dotnet tool install --global dotnet-ef --version 9.0.13
```

Verify everything is ready:

```powershell
dotnet --version                                    # 9.0.x
sqlcmd -S localhost -E -C -Q "SELECT @@VERSION"     # prints the SQL Server version
```

## Step 1 - Clone the repository

```powershell
git clone https://github.com/ManglamX/AssemblIQ.git
cd AssemblIQ/SmartTorque
```

## Step 2 - Store the connection string and first admin login

```powershell
cd src\SmartTorque.Web
dotnet user-secrets set "ConnectionStrings:Default" "Server=localhost;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True"
dotnet user-secrets set "Seed:AdminEmail" "admin@smarttorque.local"
dotnet user-secrets set "Seed:AdminPassword" "Admin@12345"
cd ..\..
```

If your SQL Server instance has a different name, change `Server=localhost` above (for example
`Server=localhost\SQLEXPRESS`).

## Step 3 - Restore and build

```powershell
dotnet restore SmartTorque.sln
dotnet build SmartTorque.sln
```

## Step 4 - Start the app

```powershell
$env:ASPNETCORE_ENVIRONMENT = "Development"
dotnet run --project src/SmartTorque.Web --no-launch-profile --urls "http://127.0.0.1:5084"
```

On the first start the app creates the database, applies all migrations and loads the default
data. The log should show:

```
Database migrations applied
Database seeding complete
```

## Step 5 - Sign in

Open <http://127.0.0.1:5084/Account/Login> and log in with:

| | |
|---|---|
| Email | `admin@smarttorque.local` |
| Password | `Admin@12345` |

(Use the values you set in Step 2.)

---

## Optional - create the database manually

The app does this automatically, but if you want to prepare the database yourself:

```powershell
# create the database
sqlcmd -S localhost -E -C -Q "IF DB_ID('SmartTorqueDb') IS NULL CREATE DATABASE SmartTorqueDb;"

# apply the migrations
dotnet ef database update --project src/SmartTorque.Data --startup-project src/SmartTorque.Web --context AppDbContext
```

To start over with an empty database:

```powershell
sqlcmd -S localhost -E -C -Q "ALTER DATABASE SmartTorqueDb SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE SmartTorqueDb;"
```

## Optional - run with Docker instead

Requires Docker Desktop (Linux containers).

```powershell
docker compose up -d --build

# App:  http://localhost:8080   admin@smarttorque.local / Admin@12345
# SQL:  localhost,14330         sa / SmartTorque_SA123

docker compose down      # stop (keeps the database)
docker compose down -v   # stop and delete the database
```

## Optional - run the tests

```powershell
dotnet test SmartTorque.sln
```

---

## If something goes wrong

| Problem | Fix |
|---|---|
| App stops right after starting with a database error | SQL Server is not running or the server name is wrong. Test with `sqlcmd -S localhost -E -C`. |
| No admin user is created | The `Seed:AdminEmail` / `Seed:AdminPassword` values are missing. Check `dotnet user-secrets list`. |
| `More than one DbContext was found` from `dotnet ef` | Add `--context AppDbContext` (as shown above). |
| `Failed to determine the https port for redirect` in the log | Harmless - you are running HTTP only. |
| `docker build` fails while downloading NuGet packages | Your internet connection dropped. Just run the same command again - finished steps are cached. |
