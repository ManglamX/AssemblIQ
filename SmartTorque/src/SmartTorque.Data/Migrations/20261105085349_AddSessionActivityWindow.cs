using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionActivityWindow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SessionActivityWindows",
                columns: table => new
                {
                    SessionActivityWindowId = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    SessionId = table.Column<long>(type: "bigint", nullable: false),
                    DeviceId = table.Column<int>(type: "int", nullable: true),
                    WorkerId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    WindowStart = table.Column<DateTime>(type: "datetime2", nullable: false),
                    WindowEnd = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SessionActivityWindows", x => x.SessionActivityWindowId);
                    table.ForeignKey(
                        name: "FK_SessionActivityWindows_Devices_DeviceId",
                        column: x => x.DeviceId,
                        principalTable: "Devices",
                        principalColumn: "DeviceId");
                    table.ForeignKey(
                        name: "FK_SessionActivityWindows_Sessions_SessionId",
                        column: x => x.SessionId,
                        principalTable: "Sessions",
                        principalColumn: "SessionId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SessionActivityWindows_DeviceId",
                table: "SessionActivityWindows",
                column: "DeviceId");

            migrationBuilder.CreateIndex(
                name: "IX_SessionActivityWindows_SessionId",
                table: "SessionActivityWindows",
                column: "SessionId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SessionActivityWindows");
        }
    }
}