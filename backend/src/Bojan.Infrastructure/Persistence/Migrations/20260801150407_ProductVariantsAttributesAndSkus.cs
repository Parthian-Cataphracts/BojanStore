using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ProductVariantsAttributesAndSkus : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_attributes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Values = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    IsFilterable = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_attributes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "product_skus",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    Code = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    Barcode = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: true),
                    Combination = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Price = table.Column<long>(type: "bigint", nullable: false),
                    Stock = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_skus", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_attributes_ProductId",
                table: "product_attributes",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_skus_Code",
                table: "product_skus",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_product_skus_ProductId",
                table: "product_skus",
                column: "ProductId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_attributes");

            migrationBuilder.DropTable(
                name: "product_skus");
        }
    }
}
