using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddJobUnitReworkLog : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LatestReworkReason",
                table: "JobUnits",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ReworkCount",
                table: "JobUnits",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "JobUnitReworks",
                columns: table => new
                {
                    JobUnitReworkId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    JobUnitId = table.Column<int>(type: "int", nullable: false),
                    JobId = table.Column<int>(type: "int", nullable: false),
                    UnitIndex = table.Column<int>(type: "int", nullable: false),
                    TokenNo = table.Column<string>(type: "nchar(40)", fixedLength: true, maxLength: 40, nullable: false),
                    ReworkIteration = table.Column<int>(type: "int", nullable: false),
                    Reason = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DefectCategory = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TargetTemplateIds = table.Column<string>(type: "nvarchar(250)", maxLength: 250, nullable: true),
                    InitiatedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: false),
                    InitiatedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    InitiatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Status = table.Column<byte>(type: "tinyint", nullable: false),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "nvarchar(450)", maxLength: 450, nullable: true),
                    CompletedByUserName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResolutionNotes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    FinalResultResultId = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobUnitReworks", x => x.JobUnitReworkId);
                    table.ForeignKey(
                        name: "FK_JobUnitReworks_JobUnits_JobUnitId",
                        column: x => x.JobUnitId,
                        principalTable: "JobUnits",
                        principalColumn: "JobUnitId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_JobUnitReworks_Jobs_JobId",
                        column: x => x.JobId,
                        principalTable: "Jobs",
                        principalColumn: "JobId");
                    table.ForeignKey(
                        name: "FK_JobUnitReworks_SessionResults_FinalResultResultId",
                        column: x => x.FinalResultResultId,
                        principalTable: "SessionResults",
                        principalColumn: "ResultId");
                });

            migrationBuilder.CreateIndex(
                name: "IX_JobUnitReworks_FinalResultResultId",
                table: "JobUnitReworks",
                column: "FinalResultResultId");

            migrationBuilder.CreateIndex(
                name: "IX_JobUnitReworks_JobId",
                table: "JobUnitReworks",
                column: "JobId");

            migrationBuilder.CreateIndex(
                name: "IX_JobUnitReworks_JobUnitId_ReworkIteration",
                table: "JobUnitReworks",
                columns: new[] { "JobUnitId", "ReworkIteration" });

            migrationBuilder.CreateIndex(
                name: "IX_JobUnitReworks_TokenNo",
                table: "JobUnitReworks",
                column: "TokenNo");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "JobUnitReworks");

            migrationBuilder.DropColumn(
                name: "LatestReworkReason",
                table: "JobUnits");

            migrationBuilder.DropColumn(
                name: "ReworkCount",
                table: "JobUnits");
        }
    }
}
