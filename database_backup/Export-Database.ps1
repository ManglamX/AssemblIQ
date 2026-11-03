# Export-Database.ps1
# Creates a fresh backup of SmartTorqueDb to database_backup\SmartTorqueDb.bak

$ErrorActionPreference = "Stop"
$scriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$publicBak = "C:\Users\Public\SmartTorqueDb.bak"
$destBak = Join-Path $scriptDir "SmartTorqueDb.bak"

Write-Host "Creating backup of SmartTorqueDb..." -ForegroundColor Cyan

sqlcmd -S localhost -d master -E -C -Q "BACKUP DATABASE SmartTorqueDb TO DISK = '$publicBak' WITH FORMAT, INIT, STATS = 20;"

if ($LASTEXITCODE -eq 0) {
    Copy-Item $publicBak -Destination $destBak -Force
    $sizeMb = [math]::Round(((Get-Item $destBak).Length / 1MB), 2)
    Write-Host "Backup created successfully: $destBak ($sizeMb MB)" -ForegroundColor Green
} else {
    Write-Host "Backup failed!" -ForegroundColor Red
}
