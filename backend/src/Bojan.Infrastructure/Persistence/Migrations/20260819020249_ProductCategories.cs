using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// A product may be filed under more than one category.
    /// </summary>
    /// <remarks>
    /// <c>products.CategoryId</c> stays where it is and keeps its meaning: it
    /// is the primary category — the one the breadcrumb walks up and the
    /// product card names. This table is every category the product is filed
    /// under, the primary included at <c>SortOrder</c> zero, and it is what
    /// browsing and the category counts read.
    /// </remarks>
    public partial class ProductCategories : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_categories",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    CategoryId = table.Column<Guid>(type: "uuid", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_categories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_categories_categories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "categories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_product_categories_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_CategoryId",
                table: "product_categories",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_product_categories_ProductId_CategoryId",
                table: "product_categories",
                columns: new[] { "ProductId", "CategoryId" },
                unique: true);

            // Every product that already exists gets the row for the category
            // it is already in. Without this the table would be empty on an
            // existing shop and every listing that now reads it would come back
            // empty — the migration has to leave the catalogue browsable, not
            // merely leave the schema right.
            //
            // Soft-deleted products included: they are restorable from the
            // panel, and one restored without its filing would come back
            // invisible.
            migrationBuilder.Sql("""
                INSERT INTO product_categories ("Id", "ProductId", "CategoryId", "SortOrder")
                SELECT gen_random_uuid(), p."Id", p."CategoryId", 0
                FROM products p
                ON CONFLICT DO NOTHING;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_categories");
        }
    }
}
