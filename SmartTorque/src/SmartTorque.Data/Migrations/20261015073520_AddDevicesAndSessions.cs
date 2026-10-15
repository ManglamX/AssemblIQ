using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDevicesAndSessions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Devices",
                columns: table => new
                {
                    DeviceId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransmitterId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    InstrumentType = table.Column<byte>(type: "tinyint", nullable: false),
                    Location = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    FirmwareVersion = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CalibrationDue = table.Column<DateOnly>(type: "date", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Devices", x => x.DeviceId);
                });

            migrationBuilder.CreateTable(
                name: "Sessions",
                columns: table => new
                {
                    SessionId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    ProductComponentId = table.Column<int>(type: "int", nullable: false),
                    UnitNo = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ComponentInstanceNo = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    WorkerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    TorqueDeviceId = table.Column<int>(type: "int", nullable: true),
                    VernierDeviceId = table.Column<int>(type: "int", nullable: true),
                    StartTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EndTime = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    FinalResult = table.Column<byte>(type: "tinyint", nullable: false),
                    CurrentStepSequence = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    ReworkCount = table.Column<int>(type: "int", nullable: false),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Sessions", x => x.SessionId);
                    table.ForeignKey(
                        name: "FK_Sessions_AspNetUsers_WorkerId",
                        column: x => x.WorkerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Devices_TorqueDeviceId",
                        column: x => x.TorqueDeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Devices_VernierDeviceId",
                        column: x => x.VernierDeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Sessions_ProductComponents_ProductComponentId",
                        column: x => x.ProductComponentId,
                        principalTable: "ProductComponents",
                        principalColumn: "ProductComponentId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SessionResults",
                columns: table => new
                {
                    ResultId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    StepId = table.Column<int>(type: "int", nullable: false),
                    InstrumentType = table.Column<byte>(type: "tinyint", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: true),
                    AttemptNo = table.Column<int>(type: "int", nullable: false),
                    TargetValue = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    TolerancePlus = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    ToleranceMinus = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    ToleranceMode = table.Column<byte>(type: "tinyint", nullable: false),
                    LowerLimit = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    UpperLimit = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    ActualValue = table.Column<decimal>(type: "decimal(10,3)", nullable: false),
                    Result = table.Column<byte>(type: "tinyint", nullable: false),
                    IsManualEntry = table.Column<bool>(type: "bit", nullable: false),
                    ManualApprovedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    ReasonCode = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    DeviceSeq = table.Column<long>(type: "bigint", nullable: true),
                    CapturedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionResults", x => x.ResultId);
                    table.ForeignKey(
                        name: "FK_SessionResults_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SessionResults_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_SessionResults_TemplateSteps_StepId",
                        column: x => x.StepId,
                        principalTable: "TemplateSteps",
                        principalColumn: "TemplateStepId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Devices_TransmitterId",
                table: "Devices",
                column: "TransmitterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SessionResults_DeviceId_DeviceSeq",
                table: "SessionResults",
                columns: new[] { "DeviceId", "DeviceSeq" },
                unique: true,
                filter: "[DeviceSeq] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_SessionResults_SessionId_StepId_AttemptNo",
                table: "SessionResults",
                columns: new[] { "SessionId", "StepId", "AttemptNo" });

            migrationBuilder.CreateIndex(
                name: "IX_SessionResults_StepId",
                table: "SessionResults",
                column: "StepId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_JobId_ProductComponentId_UnitNo_ComponentInstanceNo",
                table: "Sessions",
                columns: new[] { "JobId", "ProductComponentId", "UnitNo", "ComponentInstanceNo" },
                unique: true,
                filter: "[Status] = 1");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_ProductComponentId",
                table: "Sessions",
                column: "ProductComponentId");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_TorqueDeviceId_StartTime",
                table: "Sessions",
                columns: new[] { "TorqueDeviceId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_VernierDeviceId_StartTime",
                table: "Sessions",
                columns: new[] { "VernierDeviceId", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_WorkerId_StartTime",
                table: "Sessions",
                columns: new[] { "WorkerId", "StartTime" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionResults");

            migrationBuilder.DropTable(
                name: "Sessions");

            migrationBuilder.DropTable(
                name: "Devices");
        }
    }
}