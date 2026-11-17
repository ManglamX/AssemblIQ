# AssemblIQ / SmartTorque Database Backup Package
**Generated**: 2026-11-16  
**Source Database**: `SmartTorqueDb` on Microsoft SQL Server 2025  

This backup package contains everything required to share, inspect, and restore the complete AssemblIQ database across any environment.

---

## Package Contents

| File / Folder | Size | Description |
| :--- | :--- | :--- |
| `SmartTorqueDb_Backup_20261116.bak` | ~7.1 MB | **Native Full Database Backup** (compressed). Ideal for 1-click restore on Microsoft SQL Server 2025. |
| `SmartTorqueDb_Schema_Idempotent.sql` | ~73 KB | **Universal DDL Schema Script**. Creates all tables, foreign keys, indexes, and EF Core migration history. Runs on any SQL Server (2017, 2019, 2022, 2025, Azure SQL, Docker). |
| `SmartTorqueDb_Data_Inserts.sql` | ~2.4 MB | **Universal Data Script**. Complete `INSERT INTO` statements with `IDENTITY_INSERT` for all 29 data tables (Jobs, Sessions, BmRows, Users, Hierarchy, Devices, etc.). |
| `Data_Export_JSON/` | ~1.9 MB | **29 Table JSON Dumps**. Portable JSON exports of every table for lightweight inspection, analytics, or ingestion into non-SQL systems. |

---

## How to Restore

### Option 1: Restore Native `.bak` (Recommended for SQL Server 2025)

Using **SQL Server Management Studio (SSMS)**:
1. Open SSMS and connect to your SQL Server instance.
2. Right-click **Databases** > **Restore Database...**
3. Select **Device**, click `...`, add `SmartTorqueDb_Backup_20261116.bak`.
4. Click **OK** to restore.

Using **PowerShell / `sqlcmd`**:
```sql
RESTORE DATABASE [SmartTorqueDb]
FROM DISK = N'C:\path\to\SmartTorqueDb_Backup_20261116.bak'
WITH REPLACE, RECOVERY;
GO
```

---

### Option 2: Universal SQL Script Restore (Any SQL Server Version: 2017, 2019, 2022, 2025, Azure SQL, Docker)

If you are running an earlier version of SQL Server or Azure SQL where `.bak` compatibility is restricted:
1. Create an empty database:
   ```sql
   CREATE DATABASE [SmartTorqueDb];
   GO
   ```
2. Execute the schema script:
   ```powershell
   sqlcmd -S localhost -d SmartTorqueDb -E -C -i "SmartTorqueDb_Schema_Idempotent.sql"
   ```
3. Execute the data script:
   ```powershell
   sqlcmd -S localhost -d SmartTorqueDb -E -C -i "SmartTorqueDb_Data_Inserts.sql"
   ```

---

### Key Data Summary in this Backup
- **Hierarchy & Plant Structure**: Godrej Plant-01, Departments (`MECH-ASM`, `ENGG-ASM`, `TECH-CELL`), 7 Assembly Lines, 3 Work Stations.
- **Logins & Roles**: Admin (`admin@godrej.com` / `plant17.admin@godrej.com`), Supervisors, Workers (`worker.engg@godrej.com`, `worker.sdlc@godrej.com`, etc.).
- **Products & Templates**: Defender-PRO D14D+1CL, Safe components, 13 Blueprints & Templates, 58 Step Definitions.
- **Jobs & Sessions**: Active production jobs (`JOB-20261106-0002-ASM-01-P1`), 37 Unit Tokens, 90 Calibrated Session Measurements.
- **Bill of Materials (BOM)**: 5,761 BOM item rows across 11 revisions.
