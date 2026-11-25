using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDepartmentIsolationToDevicesAndProtocols : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Devices",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "BleProtocols",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Devices_DepartmentId",
                table: "Devices",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_BleProtocols_DepartmentId",
                table: "BleProtocols",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_BleProtocols_Departments_DepartmentId",
                table: "BleProtocols",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Devices_Departments_DepartmentId",
                table: "Devices",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_BleProtocols_Departments_DepartmentId",
                table: "BleProtocols");

            migrationBuilder.DropForeignKey(
                name: "FK_Devices_Departments_DepartmentId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_Devices_DepartmentId",
                table: "Devices");

            migrationBuilder.DropIndex(
                name: "IX_BleProtocols_DepartmentId",
                table: "BleProtocols");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Devices");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "BleProtocols");
        }
    }
}
