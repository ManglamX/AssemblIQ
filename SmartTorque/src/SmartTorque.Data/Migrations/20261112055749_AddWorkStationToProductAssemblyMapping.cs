using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddWorkStationToProductAssemblyMapping : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_ProductId_IsDefault",
                table: "ProductAssemblyMappings");

            migrationBuilder.AddColumn<int>(
                name: "WorkStationId",
                table: "ProductAssemblyMappings",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId_WorkStationId",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "AssemblyLineId", "WorkStationId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_WorkStationId",
                table: "ProductAssemblyMappings",
                column: "WorkStationId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductAssemblyMappings_WorkStations_WorkStationId",
                table: "ProductAssemblyMappings",
                column: "WorkStationId",
                principalTable: "WorkStations",
                principalColumn: "WorkStationId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductAssemblyMappings_WorkStations_WorkStationId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId_WorkStationId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_WorkStationId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "WorkStationId",
                table: "ProductAssemblyMappings");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "AssemblyLineId" });

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_IsDefault",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "IsDefault" });
        }
    }
}
