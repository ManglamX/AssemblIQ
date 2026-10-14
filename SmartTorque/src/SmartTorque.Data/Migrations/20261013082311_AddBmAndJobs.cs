using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBmAndJobs : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BmUploads",
                columns: table => new
                {
                    UploadId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    FileName = table.Column<string>(type: "nvarchar(260)", maxLength: 260, nullable: false),
                    StoredPath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    UploadedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RowCount = table.Column<int>(type: "int", nullable: false),
                    ErrorCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmUploads", x => x.UploadId);
                });

            migrationBuilder.CreateTable(
                name: "PriorityColourMap",
                columns: table => new
                {
                    MapId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ColourHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: false),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false),
                    Label = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    MatchTolerance = table.Column<int>(type: "int", nullable: false, defaultValue: 40),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PriorityColourMap", x => x.MapId);
                });

            migrationBuilder.CreateTable(
                name: "BmRows",
                columns: table => new
                {
                    RowId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UploadId = table.Column<int>(type: "int", nullable: false),
                    ExcelRowNo = table.Column<int>(type: "int", nullable: false),
                    ProductCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ColourHex = table.Column<string>(type: "nvarchar(9)", maxLength: 9, nullable: true),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)5),
                    Remarks = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ImportStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    ImportMessage = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BmRows", x => x.RowId);
                    table.ForeignKey(
                        name: "FK_BmRows_BmUploads_UploadId",
                        column: x => x.UploadId,
                        principalTable: "BmUploads",
                        principalColumn: "UploadId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Jobs",
                columns: table => new
                {
                    JobId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobNo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    ProductSerial = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Quantity = table.Column<int>(type: "int", nullable: false, defaultValue: 1),
                    QuantityCompleted = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    Priority = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)5),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignmentMode = table.Column<byte>(type: "tinyint", nullable: false),
                    AssignedWorkerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    ExecutionMode = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    SourceBmRowId = table.Column<int>(type: "int", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Jobs", x => x.JobId);
                    table.ForeignKey(
                        name: "FK_Jobs_AspNetUsers_AssignedWorkerId",
                        column: x => x.AssignedWorkerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Jobs_BmRows_SourceBmRowId",
                        column: x => x.SourceBmRowId,
                        principalTable: "BmRows",
                        principalColumn: "RowId",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Jobs_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Notifications",
                columns: table => new
                {
                    NotificationId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Type = table.Column<byte>(type: "tinyint", nullable: false),
                    Message = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    JobId = table.Column<int>(type: "int", nullable: true),
                    TargetRole = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ReadAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Notifications", x => x.NotificationId);
                    table.ForeignKey(
                        name: "FK_Notifications_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BmRows_ProductCode_DueDate",
                table: "BmRows",
                columns: new[] { "ProductCode", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_BmRows_UploadId",
                table: "BmRows",
                column: "UploadId");

            migrationBuilder.CreateIndex(
                name: "IX_BmUploads_UploadedAt",
                table: "BmUploads",
                column: "UploadedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssignedWorkerId_Status",
                table: "Jobs",
                columns: new[] { "AssignedWorkerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_JobNo",
                table: "Jobs",
                column: "JobNo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProductId",
                table: "Jobs",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_SourceBmRowId",
                table: "Jobs",
                column: "SourceBmRowId");

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_Status_Priority_DueDate",
                table: "Jobs",
                columns: new[] { "Status", "Priority", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_CreatedAt",
                table: "Notifications",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_JobId",
                table: "Notifications",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_PriorityColourMap_Priority",
                table: "PriorityColourMap",
                column: "Priority");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Notifications");

            migrationBuilder.DropTable(
                name: "PriorityColourMap");

            migrationBuilder.DropTable(
                name: "Jobs");

            migrationBuilder.DropTable(
                name: "BmRows");

            migrationBuilder.DropTable(
                name: "BmUploads");
        }
    }
}