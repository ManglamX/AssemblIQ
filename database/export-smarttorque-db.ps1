# Exports the SmartTorqueDb (schema + data) to a plain SQL script that
# anyone can restore with one sqlcmd command after cloning the repo:
#
#     sqlcmd -S localhost -E -C -i database\SmartTorqueDb-full.sql
#
# Re-run this script whenever the local data should be refreshed in git.
param(
    [string]$Server = "localhost",
    [string]$Database = "SmartTorqueDb",
    [string]$OutFile = (Join-Path $PSScriptRoot "SmartTorqueDb-full.sql")
)

$ErrorActionPreference = "Stop"
$ci = [System.Globalization.CultureInfo]::InvariantCulture
Import-Module SQLPS -DisableNameChecking

$srv = New-Object Microsoft.SqlServer.Management.Smo.Server $Server
$db = $srv.Databases[$Database]
if (-not $db) { throw "Database '$Database' was not found on server '$Server'." }
try { $srv.ConnectionContext.Connect() } catch { }

# FK-safe insert order (children after parents).
$order = @(
    "Settings", "AspNetRoles", "AspNetUsers", "AspNetUserRoles", "AspNetRoleClaims",
    "AspNetUserClaims", "AspNetUserLogins", "AspNetUserTokens",
    "Templates", "TemplateSteps", "Products", "ProductComponents",
    "BmUploads", "BmRows", "PriorityColourMap", "Jobs", "Notifications",
    "Devices", "Sessions", "SessionResults", "JobUnits",
    "ApiKeyDirectoryEntries", "ApiKeyAccessGrants",
    "AssemblyLines", "ProductAssemblyMappings", "AuditLogs",
    "__EFMigrationsHistory"
)

$tableNames = @() + $db.Tables | ForEach-Object { $_.Name }
$missing = $tableNames | Where-Object { $order -notcontains $_ }
if ($missing) { throw "Tables unknown to the export order, add them: $($missing -join ', ')" }
$wanted = @($order) | ForEach-Object { $db.Tables[$_] } | Where-Object { $_ }
if ($wanted.Count -ne $tableNames.Count) {
    throw "Table count mismatch: want $($tableNames.Count), found $($wanted.Count)."
}

$lines = New-Object System.Collections.Generic.List[string]

# ---- schema pass (SMO) ----
$opt = New-Object Microsoft.SqlServer.Management.Smo.ScriptingOptions
$opt.ScriptSchema = $true
$opt.ScriptData = $false
$opt.ScriptDrops = $false
$opt.WithDependencies = $true
$opt.ScriptBatchTerminator = $true
$opt.IncludeHeaders = $false
$scripter = New-Object Microsoft.SqlServer.Management.Smo.Scripter $srv
$scripter.Options = $opt
foreach ($stm in $scripter.Script($wanted)) {
    $lines.Add($stm)
    if ($stm -ne "GO") { $lines.Add("GO") }
}

# ---- data pass (ADO.NET -> INSERT statements) ----
$cs = "Server=$Server;Database=$Database;Integrated Security=True;TrustServerCertificate=True"
$conn = New-Object System.Data.SqlClient.SqlConnection($cs)
$conn.Open()

foreach ($t in $wanted) {
    if ($t.RowCount -eq 0) { continue }
    $name = $t.Name

    $lines.Add("")
    $lines.Add("---- Data for dbo.[$name]")
    $hasIdentity = ($t.Columns | Where-Object { $_.Identity } | Select-Object -First 1) -ne $null
    if ($hasIdentity) { $lines.Add("SET IDENTITY_INSERT dbo.[$name] ON") }

    $cols = @() + $t.Columns |
        Where-Object { ($_.DataType.SqlDataType.ToString() -match 'Rowversion|Timestamp') -eq $false } |
        ForEach-Object { $_.Name }
    $colSql = ($cols | ForEach-Object { "[$_]" }) -join ", "

    $cmd = $conn.CreateCommand()
    $cmd.CommandText = "SELECT $colSql FROM dbo.[$name]"
    $reader = $cmd.ExecuteReader()
    while ($reader.Read()) {
        $vals = New-Object System.Collections.Generic.List[string]
        for ($i = 0; $i -lt $cols.Count; $i++) {
            $v = $reader.GetValue($i)
            if ($v -is [System.DBNull]) { $vals.Add("NULL"); continue }
            if ($v -is [bool]) { $vals.Add($(if ($v) { "1" } else { "0" })); continue }
            if ($v -is [byte] -or $v -is [System.Int16] -or $v -is [int] -or $v -is [long] -or
                $v -is [float] -or $v -is [double]) { $vals.Add($v.ToString($ci)); continue }
            if ($v -is [decimal]) { $vals.Add($v.ToString($ci)); continue }
            if ($v -is [Guid]) { $vals.Add((("N'" + $v.ToString()) + "'")); continue }
            if ($v -is [DateTime]) {
                $d = $v.ToString("yyyy-MM-dd HH:mm:ss.fffffff", $ci)
                $vals.Add((("N'" + $d) + "'")); continue }
            if ($v -is [string] -or $v -is [char]) {
                $s = ($v -replace "'", "''")
                $vals.Add((("N'" + $s) + "'")); continue }
            if ($v -is [byte[]]) {
                $hex = ($v | ForEach-Object { $_.ToString("X2", $ci) }) -join ""
                $vals.Add("0x" + $hex); continue }
            throw "Unhandled data type '$($v.GetType().Name)' in dbo.[$name].[$($cols[$i])]"
        }
        $lines.Add("INSERT INTO dbo.[$name] ($colSql) VALUES ($($vals -join ', '))")
    }
    $reader.Close()
    if ($hasIdentity) { $lines.Add("SET IDENTITY_INSERT dbo.[$name] OFF") }
    $lines.Add("GO")
}
$conn.Close()

# ---- assemble: creates the database itself -> one-command restore ----
$header = "IF DB_ID(N'$Database') IS NULL`r`n    CREATE DATABASE [$Database];`r`nGO`r`nUSE [$Database];`r`nGO`r`n"
$header + (($lines -join "`r`n") + "`r`n") | Out-File $OutFile -Encoding utf8

$rows = ($lines | Where-Object { $_ -like "INSERT INTO*" }).Count
"Exported $Database -> $OutFile ($([Math]::Round((Get-Item $OutFile).Length / 1KB, 1)) KB, $rows INSERTs, $($wanted.Count) tables)."