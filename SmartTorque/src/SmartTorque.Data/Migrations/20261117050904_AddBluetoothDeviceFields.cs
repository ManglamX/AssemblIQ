using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBluetoothDeviceFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Note: IntegrityHash (JobUnits) and LastSeenSeq (Devices) already exist in DB;
            // only BleUuid and IsBluetoothDevice are new columns here.

            migrationBuilder.AddColumn<string>(
                name: "BleUuid",
                table: "Devices",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsBluetoothDevice",
                table: "Devices",
                type: "bit",
                nullable: false,
                defaultValue: false);

            // Idempotent index creation — skip if already exists
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_WorkStations_AssemblyLineId_IsActive' AND object_id = OBJECT_ID('WorkStations'))
                BEGIN
                    CREATE INDEX [IX_WorkStations_AssemblyLineId_IsActive] ON [WorkStations] ([AssemblyLineId], [IsActive]);
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Sessions_JobId_Status' AND object_id = OBJECT_ID('Sessions'))
                BEGIN
                    CREATE INDEX [IX_Sessions_JobId_Status] ON [Sessions] ([JobId], [Status]);
                END");

            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductAssemblyMappings_AssemblyLineId' AND object_id = OBJECT_ID('ProductAssemblyMappings'))
                BEGIN
                    DROP INDEX [IX_ProductAssemblyMappings_AssemblyLineId] ON [ProductAssemblyMappings];
                END");

            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_ProductAssemblyMappings_AssemblyLineId_WorkStationId' AND object_id = OBJECT_ID('ProductAssemblyMappings'))
                BEGIN
                    CREATE INDEX [IX_ProductAssemblyMappings_AssemblyLineId_WorkStationId] ON [ProductAssemblyMappings] ([AssemblyLineId], [WorkStationId]);
                END");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_WorkStations_AssemblyLineId_IsActive",
                table: "WorkStations");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_JobId_Status",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_AssemblyLineId_WorkStationId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "IntegrityHash",
                table: "JobUnits");

            migrationBuilder.DropColumn(
                name: "BleUuid",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "IsBluetoothDevice",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "LastSeenSeq",
                table: "Devices");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_AssemblyLineId",
                table: "ProductAssemblyMappings",
                column: "AssemblyLineId");
        }
    }
}
