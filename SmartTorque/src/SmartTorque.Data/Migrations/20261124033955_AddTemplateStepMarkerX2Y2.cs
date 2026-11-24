using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateStepMarkerX2Y2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "MarkerX2",
                table: "TemplateSteps",
                type: "decimal(6,3)",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "MarkerY2",
                table: "TemplateSteps",
                type: "decimal(6,3)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "MarkerX2",
                table: "TemplateSteps");

            migrationBuilder.DropColumn(
                name: "MarkerY2",
                table: "TemplateSteps");
        }
    }
}
