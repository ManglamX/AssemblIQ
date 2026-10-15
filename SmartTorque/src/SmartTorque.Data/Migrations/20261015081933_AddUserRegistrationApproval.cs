using System;

using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddUserRegistrationApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte>(
                name: "RegistrationStatus",
                table: "AspNetUsers",
                type: "tinyint",
                nullable: false,
                defaultValue: (byte)2);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReason",
                table: "AspNetUsers",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RequestedRole",
                table: "AspNetUsers",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewedAt",
                table: "AspNetUsers",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewedBy",
                table: "AspNetUsers",
                type: "nvarchar(256)",
                maxLength: 256,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "RegistrationStatus",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RejectionReason",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "RequestedRole",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewedAt",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "ReviewedBy",
                table: "AspNetUsers");
        }
    }
}