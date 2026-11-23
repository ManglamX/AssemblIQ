using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddTemplateDepartmentIsolation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DepartmentId",
                table: "Templates",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Templates_DepartmentId",
                table: "Templates",
                column: "DepartmentId");

            migrationBuilder.AddForeignKey(
                name: "FK_Templates_Departments_DepartmentId",
                table: "Templates",
                column: "DepartmentId",
                principalTable: "Departments",
                principalColumn: "DepartmentId",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Templates_Departments_DepartmentId",
                table: "Templates");

            migrationBuilder.DropIndex(
                name: "IX_Templates_DepartmentId",
                table: "Templates");

            migrationBuilder.DropColumn(
                name: "DepartmentId",
                table: "Templates");
        }
    }
}
