@echo off
setlocal
echo ========================================================
echo  TURN ON: Remote Database Sharing
echo ========================================================
echo.

:: 1. Enable SQL Login
sqlcmd -S localhost -d master -E -C -Q "ALTER LOGIN [smarttorque_dev] ENABLE;" >nul 2>&1
if %errorLevel% equ 0 (
    echo [OK] SQL Login 'smarttorque_dev' is ENABLED.
) else (
    echo [!] Could not update SQL Login. Make sure SQL Server is running.
)

:: 2. Enable Firewall Rule (if administrator or via netsh)
netsh advfirewall firewall set rule name="SQL Server (Port 1433)" new enable=yes >nul 2>&1
if %errorLevel% equ 0 (
    echo [OK] Windows Firewall Port 1433 is OPEN.
) else (
    echo [i] Note: Run as Administrator if you wish to toggle the Firewall rule automatically.
)

echo.
echo --------------------------------------------------------
echo  STATUS: SHARING IS ACTIVE
echo --------------------------------------------------------
echo  Your colleague can connect using:
echo  Server=172.24.240.126,1433;Database=SmartTorqueDb;User Id=smarttorque_dev;Password=TorqueDev@2026!;TrustServerCertificate=True;
echo.
pause
