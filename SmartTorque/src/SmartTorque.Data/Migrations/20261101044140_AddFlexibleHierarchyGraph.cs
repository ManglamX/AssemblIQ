using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SmartTorque.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFlexibleHierarchyGraph : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AssignedSupervisorId",
                table: "Jobs",
                type: "nvarchar(450)",
                maxLength: 450,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "HierarchyNodeId",
                table: "AspNetUsers",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "HierarchyNodes",
                columns: table => new
                {
                    NodeId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentNodeId = table.Column<int>(type: "int", nullable: true),
                    Code = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    NodeType = table.Column<byte>(type: "tinyint", nullable: false),
                    Path = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Depth = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    AdminUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    IsActive = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HierarchyNodes", x => x.NodeId);
                    table.ForeignKey(
                        name: "FK_HierarchyNodes_AspNetUsers_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.NoAction);
                    table.ForeignKey(
                        name: "FK_HierarchyNodes_HierarchyNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "HierarchyNodes",
                        principalColumn: "NodeId",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HierarchyNodeRequests",
                columns: table => new
                {
                    RequestId = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ParentNodeId = table.Column<int>(type: "int", nullable: false),
                    RequestedNodeName = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    RequestedNodeType = table.Column<byte>(type: "tinyint", nullable: false),
                    RequestedByUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RequestStatus = table.Column<byte>(type: "tinyint", nullable: false, defaultValue: (byte)1),
                    ApproverUserId = table.Column<string>(type: "nvarchar(450)", nullable: true),
                    RejectionReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HierarchyNodeRequests", x => x.RequestId);
                    table.ForeignKey(
                        name: "FK_HierarchyNodeRequests_AspNetUsers_ApproverUserId",
                        column: x => x.ApproverUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HierarchyNodeRequests_AspNetUsers_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_HierarchyNodeRequests_HierarchyNodes_ParentNodeId",
                        column: x => x.ParentNodeId,
                        principalTable: "HierarchyNodes",
                        principalColumn: "NodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Jobs_AssignedSupervisorId",
                table: "Jobs",
                column: "AssignedSupervisorId");

            migrationBuilder.CreateIndex(
                name: "IX_AspNetUsers_HierarchyNodeId",
                table: "AspNetUsers",
                column: "HierarchyNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodeRequests_ApproverUserId",
                table: "HierarchyNodeRequests",
                column: "ApproverUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodeRequests_ParentNodeId",
                table: "HierarchyNodeRequests",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodeRequests_RequestedByUserId",
                table: "HierarchyNodeRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_AdminUserId",
                table: "HierarchyNodes",
                column: "AdminUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_Code",
                table: "HierarchyNodes",
                column: "Code");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_ParentNodeId",
                table: "HierarchyNodes",
                column: "ParentNodeId");

            migrationBuilder.CreateIndex(
                name: "IX_HierarchyNodes_Path",
                table: "HierarchyNodes",
                column: "Path");

            migrationBuilder.AddForeignKey(
                name: "FK_AspNetUsers_HierarchyNodes_HierarchyNodeId",
                table: "AspNetUsers",
                column: "HierarchyNodeId",
                principalTable: "HierarchyNodes",
                principalColumn: "NodeId",
                onDelete: ReferentialAction.NoAction);

            migrationBuilder.AddForeignKey(
                name: "FK_Jobs_AspNetUsers_AssignedSupervisorId",
                table: "Jobs",
                column: "AssignedSupervisorId",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.NoAction);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AspNetUsers_HierarchyNodes_HierarchyNodeId",
                table: "AspNetUsers");

            migrationBuilder.DropForeignKey(
                name: "FK_Jobs_AspNetUsers_AssignedSupervisorId",
                table: "Jobs");

            migrationBuilder.DropTable(
                name: "HierarchyNodeRequests");

            migrationBuilder.DropTable(
                name: "HierarchyNodes");

            migrationBuilder.DropIndex(
                name: "IX_Jobs_AssignedSupervisorId",
                table: "Jobs");

            migrationBuilder.DropIndex(
                name: "IX_AspNetUsers_HierarchyNodeId",
                table: "AspNetUsers");

            migrationBuilder.DropColumn(
                name: "AssignedSupervisorId",
                table: "Jobs");

            migrationBuilder.DropColumn(
                name: "HierarchyNodeId",
                table: "AspNetUsers");
        }
    }
}
