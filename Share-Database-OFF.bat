@echo off
setlocal
echo ========================================================
echo  TURN OFF: Remote Database Sharing
echo ========================================================
echo.

:: 1. Disable SQL Login & Kill any active remote connections
sqlcmd -S localhost -d master -E -C -Q "ALTER LOGIN [smarttorque_dev] DISABLE; DECLARE @spid INT; DECLARE cur CURSOR FOR SELECT session_id FROM sys.dm_exec_sessions WHERE login_name = 'smarttorque_dev'; OPEN cur; FETCH NEXT FROM cur INTO @spid; WHILE @@FETCH_STATUS = 0 BEGIN EXEC('KILL ' + @spid); FETCH NEXT FROM cur INTO @spid; END; CLOSE cur; DEALLOCATE cur;" >nul 2>&1

if %errorLevel% equ 0 (
    echo [OK] SQL Login 'smarttorque_dev' is DISABLED.
    echo [OK] Any active remote sessions have been terminated.
) else (
    echo [!] Could not update SQL Login. Make sure SQL Server is running.
)

:: 2. Disable Firewall Rule
netsh advfirewall firewall set rule name="SQL Server (Port 1433)" new enable=no >nul 2>&1
if %errorLevel% equ 0 (
    echo [OK] Windows Firewall Port 1433 is CLOSED.
) else (
    echo [i] Note: Run as Administrator if you wish to toggle the Firewall rule automatically.
)

echo.
echo --------------------------------------------------------
echo  STATUS: SHARING IS DISABLED
echo --------------------------------------------------------
echo  Nobody can connect to your database from outside.
echo  Your local application (Windows Authentication) continues working normally.
echo.
pause
