using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CatalogueMerchandisingFields : Migration
    {
        /// <inheritdoc />
        /// <remarks>
        /// Three of these columns carry a default that is not the CLR default
        /// for their type, and the difference matters to every row that already
        /// exists. Scaffolded as generated, `TrackStock` would arrive as false
        /// across the whole catalogue — stock counting switched off for every
        /// product on deploy — `LowStockThreshold` as 0, so nothing is ever
        /// low, and `ShowInMenu` as false, which empties the storefront's
        /// navigation. The values below are the ones the entities declare, so
        /// an existing row keeps behaving exactly as it did before the column
        /// existed.
        /// </remarks>
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AllowBackorder",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "LowStockThreshold",
                table: "products",
                type: "integer",
                nullable: false,
                defaultValue: 5);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "products",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "products",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "TrackStock",
                table: "products",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<string>(
                name: "Note",
                table: "payment_methods",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "categories",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "categories",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ShowInMenu",
                table: "categories",
                type: "boolean",
                nullable: false,
                defaultValue: true);

            migrationBuilder.AddColumn<int>(
                name: "SortOrder",
                table: "categories",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Country",
                table: "brands",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaDescription",
                table: "brands",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MetaTitle",
                table: "brands",
                type: "character varying(300)",
                maxLength: 300,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_categories_SortOrder",
                table: "categories",
                column: "SortOrder");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_categories_SortOrder",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "AllowBackorder",
                table: "products");

            migrationBuilder.DropColumn(
                name: "LowStockThreshold",
                table: "products");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "products");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "products");

            migrationBuilder.DropColumn(
                name: "TrackStock",
                table: "products");

            migrationBuilder.DropColumn(
                name: "Note",
                table: "payment_methods");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "ShowInMenu",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "SortOrder",
                table: "categories");

            migrationBuilder.DropColumn(
                name: "Country",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "MetaDescription",
                table: "brands");

            migrationBuilder.DropColumn(
                name: "MetaTitle",
                table: "brands");
        }
    }
}
