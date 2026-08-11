using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Quantity breaks for B2B pricing.
    /// </summary>
    /// <remarks>
    /// The ladder a pro-forma prices against — "from twenty units, ten percent
    /// off". It belongs to the product rather than to the buyer because what
    /// makes a hundred units cheaper is the product's own economics: one carton,
    /// one picking run, one delivery. See ProductVolumeTier.
    ///
    /// The unique index on (product, floor) is what stops one rung being typed
    /// twice with two different discounts. The pricing has a rule for reading
    /// that, but the rule exists to survive bad data rather than to permit it.
    /// </remarks>
    public partial class ProductVolumeTiers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "product_volume_tiers",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    ProductId = table.Column<Guid>(type: "uuid", nullable: false),
                    MinimumQuantity = table.Column<int>(type: "integer", nullable: false),
                    DiscountPercent = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_product_volume_tiers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_product_volume_tiers_products_ProductId",
                        column: x => x.ProductId,
                        principalTable: "products",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_product_volume_tiers_ProductId_MinimumQuantity",
                table: "product_volume_tiers",
                columns: new[] { "ProductId", "MinimumQuantity" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_volume_tiers");
        }
    }
}
