# SmartTorque Database Synchronization Guide

This directory provides tools to keep `SmartTorqueDb` synchronized between your machine and your colleague's machine.

---

## Method 1: Instant Snapshot Clone (Offline / Standalone)

Use this method to immediately clone your entire database onto your colleague's machine.

### Step 1: Export on Your Machine (Source)
Whenever you want to produce a fresh copy of your database:
```powershell
.\database_backup\Export-Database.ps1
```
This generates `database_backup\SmartTorqueDb.bak` containing all tables, users, hierarchy, products, and jobs.

### Step 2: Restore on Your Colleague's Machine
1. Send or pull `database_backup\SmartTorqueDb.bak` and `Restore-Database.ps1` to your colleague's machine.
2. In PowerShell, your colleague runs:
   ```powershell
   .\database_backup\Restore-Database.ps1
   ```
   *(If their SQL Server instance has a specific name like `localhost\SQLEXPRESS`, pass it as: `.\Restore-Database.ps1 -ServerInstance "localhost\SQLEXPRESS"`)*
3. **Done!** Their database is now an exact replica of yours.

---

## Method 2: Real-Time Live Sync (Shared Central Database)

If both of you are on the same Wi-Fi, office LAN, or VPN (e.g. Tailscale / ZeroTier):
Instead of maintaining two disconnected databases, **both machines can connect to ONE shared database**. Any change made by either developer appears instantly on both machines in real-time.

### Step 1: Allow Remote Connections on Your Host PC
1. **Enable TCP/IP in SQL Server**:
   - Open **SQL Server Configuration Manager**.
   - Navigate to **SQL Server Network Configuration** > **Protocols for MSSQLSERVER**.
   - Right-click **TCP/IP** > **Enable**.
   - Under **IP Addresses**, ensure **TCP Port 1433** is set for `IPAll`.
   - Restart the SQL Server service.
2. **Open Firewall Port 1433**:
   Run in Administrator PowerShell:
   ```powershell
   New-NetFirewallRule -DisplayName "SQL Server (Port 1433)" -Direction Inbound -Protocol TCP -LocalPort 1433 -Action Allow
   ```
3. **Find your Local IP Address**:
   ```powershell
   (Get-NetIPAddress -AddressFamily IPv4 | Where-Object { $_.InterfaceAlias -like '*Wi-Fi*' -or $_.InterfaceAlias -like '*Ethernet*' }).IPAddress
   ```

### Step 2: Connect from Your Colleague's PC
In their `SmartTorque.Web\appsettings.json`, update the connection string to point to your IP:
```json
{
  "ConnectionStrings": {
    "Default": "Server=<YOUR_IP_ADDRESS>,1433;Database=SmartTorqueDb;Integrated Security=True;TrustServerCertificate=True;"
  }
}
```
*(Or use SQL Authentication with a shared user account).*
