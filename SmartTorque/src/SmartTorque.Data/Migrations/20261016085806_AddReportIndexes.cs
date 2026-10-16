using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddReportIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Sessions_StartTime",
                table: "Sessions",
                column: "StartTime");

            migrationBuilder.CreateIndex(
                name: "IX_Sessions_Status_StartTime",
                table: "Sessions",
                columns: new[] { "Status", "StartTime" });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_ProductSerial",
                table: "Jobs",
                column: "ProductSerial");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Sessions_StartTime",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Sessions_Status_StartTime",
                table: "Sessions");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_ProductSerial",
                table: "Jobs");
        }
    }
}