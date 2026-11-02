using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductAssemblyMappingGranularScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings");

            migrationBuilder.AddColumn<string>(
                name: "AllowedStepIds",
                table: "ProductAssemblyMappings",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AllowedTemplateIds",
                table: "ProductAssemblyMappings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeSummary",
                table: "ProductAssemblyMappings",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<byte>(
                name: "ScopeType",
                table: "ProductAssemblyMappings",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)1);

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "AssemblyLineId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "AllowedStepIds",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "AllowedTemplateIds",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "ScopeSummary",
                table: "ProductAssemblyMappings");

            migrationBuilder.DropColumn(
                name: "ScopeType",
                table: "ProductAssemblyMappings");

            migrationBuilder.CreateIndex(
                name: "IX_ProductAssemblyMappings_ProductId_AssemblyLineId",
                table: "ProductAssemblyMappings",
                columns: new[] { "ProductId", "AssemblyLineId" },
                unique: true);
        }
    }
}
