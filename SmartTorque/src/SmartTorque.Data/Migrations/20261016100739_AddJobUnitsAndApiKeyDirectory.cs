using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobUnitsAndApiKeyDirectory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsMainAdmin",
                table: "AspNetUsers",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ApiKeyDirectoryEntries",
                columns: table => new
                {
                    ApiKeyId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    KeyName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    ApiScope = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    RawApiKey = table.Column<string>(type: "nvarchar(256)", maxLength: 256, nullable: false),
                    ApiKeyHash = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyDirectoryEntries", x => x.ApiKeyId);
                });

            migrationBuilder.CreateTable(
                name: "JobUnits",
                columns: table => new
                {
                    JobUnitId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    UnitIndex = table.Column<int>(type: "int", nullable: false),
                    TokenNo = table.Column<string>(type: "nchar(40)", fixedLength: true, maxLength: 40, nullable: false),
                    ProductSerial = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByWorkerId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobUnits", x => x.JobUnitId);
                    table.ForeignKey(
                        name: "FK_JobUnits_AspNetUsers_CompletedByWorkerId",
                        column: x => x.CompletedByWorkerId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_JobUnits_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ApiKeyAccessGrants",
                columns: table => new
                {
                    GrantId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ApiKeyId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RequestedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DecidedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ApiKeyAccessGrants", x => x.GrantId);
                    table.ForeignKey(
                        name: "FK_ApiKeyAccessGrants_ApiKeyDirectoryEntries_ApiKeyId",
                        column: x => x.ApiKeyId,
                        principalTable: "ApiKeyDirectoryEntries",
                        principalColumn: "ApiKeyId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ApiKeyAccessGrants_AspNetUsers_DecidedByUserId",
                        column: x => x.DecidedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ApiKeyAccessGrants_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyAccessGrants_ApiKeyId_UserId",
                table: "ApiKeyAccessGrants",
                columns: new[] { "ApiKeyId", "UserId" });

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyAccessGrants_DecidedByUserId",
                table: "ApiKeyAccessGrants",
                column: "DecidedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyAccessGrants_UserId",
                table: "ApiKeyAccessGrants",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyDirectoryEntries_ApiKeyHash",
                table: "ApiKeyDirectoryEntries",
                column: "ApiKeyHash");

            migrationBuilder.CreateIndex(
                name: "IX_ApiKeyDirectoryEntries_KeyName",
                table: "ApiKeyDirectoryEntries",
                column: "KeyName");

            migrationBuilder.CreateIndex(
                name: "IX_JobUnits_CompletedByWorkerId",
                table: "JobUnits",
                column: "CompletedByWorkerId");

            migrationBuilder.CreateIndex(
                name: "IX_JobUnits_JobId_UnitIndex",
                table: "JobUnits",
                columns: new[] { "JobId", "UnitIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobUnits_TokenNo",
                table: "JobUnits",
                column: "TokenNo",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ApiKeyAccessGrants");

            migrationBuilder.DropTable(
                name: "JobUnits");

            migrationBuilder.DropTable(
                name: "ApiKeyDirectoryEntries");

            migrationBuilder.DropColumn(
                name: "IsMainAdmin",
                table: "AspNetUsers");
        }
    }
}
