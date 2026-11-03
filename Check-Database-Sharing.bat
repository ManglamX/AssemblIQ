@echo off
setlocal
echo ========================================================
echo  STATUS: Remote Database Sharing
echo ========================================================
echo.

sqlcmd -S localhost -d master -E -C -Q "SET NOCOUNT ON; SELECT CASE WHEN is_disabled = 0 THEN 'SQL Login (smarttorque_dev): ENABLED [Sharing Allowed]' ELSE 'SQL Login (smarttorque_dev): DISABLED [Blocked]' END AS Status FROM sys.server_principals WHERE name = 'smarttorque_dev';"

echo.
pause
