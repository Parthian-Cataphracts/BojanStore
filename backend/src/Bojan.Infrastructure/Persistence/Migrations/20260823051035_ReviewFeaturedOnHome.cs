using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReviewFeaturedOnHome : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsFeaturedOnHome",
                table: "product_reviews",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_product_reviews_Status_IsFeaturedOnHome",
                table: "product_reviews",
                columns: new[] { "Status", "IsFeaturedOnHome" },
                filter: "\"IsFeaturedOnHome\"");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_product_reviews_Status_IsFeaturedOnHome",
                table: "product_reviews");

            migrationBuilder.DropColumn(
                name: "IsFeaturedOnHome",
                table: "product_reviews");
        }
    }
}
