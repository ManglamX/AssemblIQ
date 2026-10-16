using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSalesOrderAndPoNo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PONo",
                table: "Jobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesOrder",
                table: "Jobs",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PONo",
                table: "BmRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SalesOrder",
                table: "BmRows",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_SalesOrder_PONo",
                table: "Jobs",
                columns: new[] { "SalesOrder", "PONo" });

            migrationBuilder.CreateIndex(
                name: "IX_BmRows_SalesOrder_PONo",
                table: "BmRows",
                columns: new[] { "SalesOrder", "PONo" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Jobs_SalesOrder_PONo",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_BmRows_SalesOrder_PONo",
                table: "BmRows");

            migrationBuilder.DropColumn(
                name: "PONo",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "SalesOrder",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "PONo",
                table: "BmRows");

            migrationBuilder.DropColumn(
                name: "SalesOrder",
                table: "BmRows");
        }
    }
}