using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class FixJobUnitReworkFinalResult : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobUnitReworks_SessionResults_FinalResultResultId",
                table: "JobUnitReworks");

            migrationBuilder.DropIndex(
                name: "IX_JobUnitReworks_FinalResultResultId",
                table: "JobUnitReworks");

            migrationBuilder.DropColumn(
                name: "FinalResultResultId",
                table: "JobUnitReworks");

            migrationBuilder.AddColumn<byte>(
                name: "FinalResult",
                table: "JobUnitReworks",
                type: "tinyint",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FinalResult",
                table: "JobUnitReworks");

            migrationBuilder.AddColumn<long>(
                name: "FinalResultResultId",
                table: "JobUnitReworks",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobUnitReworks_FinalResultResultId",
                table: "JobUnitReworks",
                column: "FinalResultResultId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobUnitReworks_SessionResults_FinalResultResultId",
                table: "JobUnitReworks",
                column: "FinalResultResultId",
                principalTable: "SessionResults",
                principalColumn: "ResultId");
        }
    }
}
