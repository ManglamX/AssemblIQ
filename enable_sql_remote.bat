@echo off
:: Batch script to restart SQL Server and open Port 1433 in Windows Firewall
echo ========================================================
echo Configuring SQL Server for Remote SQL Authentication
echo ========================================================
echo.

net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Administrator permissions required. Requesting elevation...
    powershell -Command "Start-Process cmd -ArgumentList '/c \"\"%~f0\"\"' -Verb RunAs"
    exit /b
)

echo [1/2] Adding Windows Firewall Rule for Port 1433...
netsh advfirewall firewall add rule name="SQL Server (Port 1433)" dir=in action=allow protocol=TCP localport=1433 >nul
echo Firewall rule for Port 1433 added successfully.

echo [2/2] Restarting SQL Server service to activate Mixed Mode Authentication...
net stop MSSQLSERVER
net start MSSQLSERVER

echo.
echo ========================================================
echo SUCCESS! SQL Server is now ready for remote connections.
echo ========================================================
pause
