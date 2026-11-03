# Restore-Database.ps1
# Restores SmartTorqueDb from database_backup\SmartTorqueDb.bak on any SQL Server instance

param (
    [string]$ServerInstance = "localhost"
)

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$sourceBak = Join-Path $scriptDir "SmartTorqueDb.bak"
$publicBak = "C:\Users\Public\SmartTorqueDb.bak"

if (-not (Test-Path $sourceBak)) {
    Write-Host "Error: Cannot find $sourceBak" -ForegroundColor Red
    exit 1
}

Write-Host "Copying backup file to $publicBak..." -ForegroundColor Cyan
Copy-Item $sourceBak -Destination $publicBak -Force

Write-Host "Restoring database SmartTorqueDb on '$ServerInstance'..." -ForegroundColor Cyan

$sqlRestore = @"
SET NOCOUNT ON;

DECLARE @dataPath NVARCHAR(500) = CAST(SERVERPROPERTY('InstanceDefaultDataPath') AS NVARCHAR(500));
DECLARE @logPath NVARCHAR(500) = CAST(SERVERPROPERTY('InstanceDefaultLogPath') AS NVARCHAR(500));

IF @dataPath IS NULL OR @dataPath = ''
    SET @dataPath = N'C:\Program Files\Microsoft SQL Server\MSSQL17.MSSQLSERVER\MSSQL\DATA\';
IF @logPath IS NULL OR @logPath = ''
    SET @logPath = @dataPath;

DECLARE @mdfPath NVARCHAR(600) = @dataPath + N'SmartTorqueDb.mdf';
DECLARE @ldfPath NVARCHAR(600) = @logPath + N'SmartTorqueDb_log.ldf';

-- Terminate existing connections if database already exists
IF DB_ID('SmartTorqueDb') IS NOT NULL
BEGIN
    ALTER DATABASE [SmartTorqueDb] SET SINGLE_USER WITH ROLLBACK IMMEDIATE;
END;

-- Restore with MOVE to match current instance file locations
RESTORE DATABASE [SmartTorqueDb] 
FROM DISK = N'$publicBak' 
WITH REPLACE,
     MOVE N'SmartTorqueDb' TO @mdfPath,
     MOVE N'SmartTorqueDb_log' TO @ldfPath,
     STATS = 20;

ALTER DATABASE [SmartTorqueDb] SET MULTI_USER;
"@

$sqlFile = Join-Path $env:TEMP "restore_smarttorque.sql"
Set-Content -Path $sqlFile -Value $sqlRestore -Encoding UTF8

sqlcmd -S $ServerInstance -d master -E -C -i $sqlFile

if ($LASTEXITCODE -eq 0) {
    Write-Host "`nDatabase SmartTorqueDb restored successfully!" -ForegroundColor Green
    Write-Host "Your database is now identical to the source database." -ForegroundColor Green
} else {
    Write-Host "`nRestore failed! Check error details above." -ForegroundColor Red
}

Remove-Item $sqlFile -ErrorAction SilentlyContinue
