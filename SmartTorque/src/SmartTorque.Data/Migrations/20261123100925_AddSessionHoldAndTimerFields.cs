using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionHoldAndTimerFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<double>(
                name: "AccumulatedActiveSeconds",
                table: "Sessions",
                type: "float",
                nullable: false,
                defaultValue: 0.0);

            migrationBuilder.AddColumn<string>(
                name: "HoldReason",
                table: "Sessions",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "HoldStartTime",
                table: "Sessions",
                type: "datetime2",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AccumulatedActiveSeconds",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "HoldReason",
                table: "Sessions");

            migrationBuilder.DropColumn(
                name: "HoldStartTime",
                table: "Sessions");
        }
    }
}
