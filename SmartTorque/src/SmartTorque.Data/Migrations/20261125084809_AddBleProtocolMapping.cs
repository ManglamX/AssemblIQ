using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBleProtocolMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BleProtocolId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "BleProtocols",
                columns: table => new
                {
                    BleProtocolId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SupportedInstrumentType = table.Column<byte>(type: "tinyint", nullable: true),
                    ServiceUuid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    TxCharUuid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    RxCharUuid = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: true),
                    DeviceNamePrefixes = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    FrameFormat = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    FrameStartFilter = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    IgnorePrefix = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Delimiter = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    RegexPattern = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    ValueField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ValueMultiplier = table.Column<double>(type: "float", nullable: false),
                    CounterField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ToolSerialField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    UnitField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AngleField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    JudgmentField = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PassValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    DoubleTighteningValue = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    SyncLimitsCommandTemplate = table.Column<string>(type: "nvarchar(120)", maxLength: 120, nullable: true),
                    IsBuiltIn = table.Column<bool>(type: "bit", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BleProtocols", x => x.BleProtocolId);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_BleProtocolId",
                table: "Devices",
                column: "BleProtocolId");

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_BleProtocols_BleProtocolId",
                table: "Devices",
                column: "BleProtocolId",
                principalTable: "BleProtocols",
                principalColumn: "BleProtocolId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Devices_BleProtocols_BleProtocolId",
                table: "Devices");

            migrationBuilder.DropTable(
                name: "BleProtocols");

            migrationBuilder.DropIndex(
                name: "IX_Devices_BleProtocolId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "BleProtocolId",
                table: "Devices");
        }
    }
}
