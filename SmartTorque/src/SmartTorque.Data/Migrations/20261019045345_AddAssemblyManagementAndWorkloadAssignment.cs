using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAssemblyManagementAndWorkloadAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssemblyLineId",
                table: "Jobs",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "Jobs",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "EstimatedWorkload",
                table: "Jobs",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 1.0m);

            migrationBuilder.AddColumn<int>(
                name: "CurrentAssemblyLineId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AssemblyLines",
                columns: table => new
                {
                    AssemblyLineId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    AssemblyCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    AssemblyName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Department = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Description = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AssemblyLines", x => x.AssemblyLineId);
                });

            migrationBuilder.CreateTable(
                name: "ProductAssemblyMappings",
                columns: table => new
                {
                    ProductAssemblyMappingId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProductId = table.Column<int>(type: "int", nullable: false),
                    AssemblyLineId = table.Column<int>(type: "int", nullable: false),
                    IsDefault = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CreatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UpdatedBy = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProductAssemblyMappings", x => x.ProductAssemblyMappingId);
                    table.ForeignKey(
                        name: "FK_ProductAssemblyMappings_AssemblyLines_AssemblyLineId",
                        column: x => x.AssemblyLineId,
                        principalTable: "AssemblyLines",
                        principalColumn: "AssemblyLineId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProductAssemblyMappings_Products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "Products",
                        principalColumn: "ProductId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssemblyLineId_Status",
                table: "Jobs",
                columns: new[] { "AssemblyLineId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_CurrentAssemblyLineId",
                table: "AspNetUsers",
                column: "CurrentAssemblyLineId");

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyLines_AssemblyCode",
                table: "AssemblyLines",
                column: "AssemblyCode",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AssemblyLines_IsActive",
                table: "AssemblyLines",
                column: "IsActive");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_AssemblyLineId",
                table: "ProductAssemblyMappings",
                column: "AssemblyLineId");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "AssemblyLineId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_IsDefault",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "IsDefault" });

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_AssemblyLines_CurrentAssemblyLineId",
                table: "AspNetUsers",
                column: "CurrentAssemblyLineId",
                principalTable: "AssemblyLines",
                principalColumn: "AssemblyLineId",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AssemblyLines_AssemblyLineId",
                table: "Jobs",
                column: "AssemblyLineId",
                principalTable: "AssemblyLines",
                principalColumn: "AssemblyLineId",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_AssemblyLines_CurrentAssemblyLineId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AssemblyLines_AssemblyLineId",
                table: "Jobs");

            migrationBuilder.DropTable(
                name: "ProductAssemblyMappings");

            migrationBuilder.DropTable(
                name: "AssemblyLines");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AssemblyLineId_Status",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_CurrentAssemblyLineId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssemblyLineId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "EstimatedWorkload",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "CurrentAssemblyLineId",
                table: "AspNetUsers");
        }
    }
}
