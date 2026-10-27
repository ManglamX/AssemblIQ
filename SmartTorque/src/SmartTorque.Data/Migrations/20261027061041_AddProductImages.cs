using System;



using Microsoft.EntityFrameworkCore.Migrations;



#nullable disable



namespace SmartTorque.Data.Migrations

{

    /// <inheritdoc />

    public partial class AddProductImages : Migration

    {

        /// <inheritdoc />

        protected override void Up(MigrationBuilder migrationBuilder)

        {

            migrationBuilder.CreateTable(

                name: "ProductImages",

                columns: table => new

                {

                    ProductImageId = table.Column<int>(type: "int", nullable: false)

                        .Annotation("SqlServer:Identity", "1, 1"),

                    ProductId = table.Column<int>(type: "int", nullable: false),

                    ImagePath = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),

                    SortOrder = table.Column<int>(type: "int", nullable: false),

                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)

                },

                constraints: table =>

                {

                    table.PrimaryKey("PK_ProductImages", x => x.ProductImageId);

                    table.ForeignKey(

                        name: "FK_ProductImages_Products_ProductId",

                        column: x => x.ProductId,

                        principalTable: "Products",

                        principalColumn: "ProductId",

                        onDelete: ReferentialAction.Cascade);

                });



            migrationBuilder.CreateIndex(

                name: "IX_ProductImages_ProductId_SortOrder",

                table: "ProductImages",

                columns: new[] { "ProductId", "SortOrder" });



            // Backfill the gallery so products saved before the gallery keep their image.

            migrationBuilder.Sql(

                "INSERT INTO ProductImages (ProductId, ImagePath, SortOrder, CreatedAt) " +

                "SELECT ProductId, ImagePath, 0, GETUTCDATE() " +

                "FROM Products " +

                "WHERE ImagePath IS NOT NULL AND ImagePath <> '';");

        }



        /// <inheritdoc />

        protected override void Down(MigrationBuilder migrationBuilder)

        {

            migrationBuilder.DropTable(

                name: "ProductImages");

        }

    }

}