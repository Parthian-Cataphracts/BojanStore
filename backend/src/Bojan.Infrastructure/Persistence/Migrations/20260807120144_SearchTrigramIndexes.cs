using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Makes the catalogue's search something other than a full table scan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Searching the shop runs four <c>LIKE '%…%'</c> comparisons — the product
    /// title, its SKU, its brand's name and its category's name. A leading
    /// wildcard cannot use an ordinary B-tree index, so every one of them read
    /// every row, and the cost grew in a straight line with the catalogue. A
    /// shop with a few dozen products never notices; a shop with a few thousand
    /// has a search box that takes seconds.
    /// </para>
    /// <para>
    /// Trigram indexes are what PostgreSQL provides for exactly this shape.
    /// They index every three-character sequence in the column, which is what
    /// makes an infix match indexable, and they work on Persian text — a
    /// tokenising full-text index would need a Persian dictionary this database
    /// does not have, and would not match part-words the way shoppers type
    /// them.
    /// </para>
    /// <para>
    /// <c>CREATE EXTENSION</c> needs the privilege to install one. If it is
    /// refused, this migration fails loudly rather than skipping the indexes
    /// quietly: a search that is still scanning is worth knowing about at
    /// deploy time, not discovering under load.
    /// </para>
    /// </remarks>
    public partial class SearchTrigramIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""CREATE EXTENSION IF NOT EXISTS pg_trgm;""");

            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_products_Title_trgm" ON products USING gin ("Title" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_products_Sku_trgm" ON products USING gin ("Sku" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_brands_Name_trgm" ON brands USING gin ("Name" gin_trgm_ops);""");
            migrationBuilder.Sql(
                """CREATE INDEX IF NOT EXISTS "IX_categories_Name_trgm" ON categories USING gin ("Name" gin_trgm_ops);""");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_categories_Name_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_brands_Name_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_products_Sku_trgm";""");
            migrationBuilder.Sql("""DROP INDEX IF EXISTS "IX_products_Title_trgm";""");

            // The extension stays. Dropping it would take down anything else in
            // the database that came to depend on it, and an unused extension
            // costs nothing.
        }
    }
}
