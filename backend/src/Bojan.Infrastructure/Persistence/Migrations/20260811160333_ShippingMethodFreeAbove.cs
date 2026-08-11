using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ShippingMethodFreeAbove : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "FreeAboveAmount",
                table: "shipping_methods",
                type: "bigint",
                nullable: true);

            // Carry over whatever the shop had already configured.
            //
            // The threshold used to be one figure on the store settings screen,
            // and it moved onto each method because a courier that is never free
            // and a post tier that is free over a million are both ordinary. A
            // null column would silently withdraw an offer the owner had set —
            // so the old value is copied onto every method first, and the owner
            // can then say which of them it really applies to.
            //
            // Only digits: the field was free text, and a figure typed with
            // Persian numerals never parsed as an amount anyway.
            migrationBuilder.Sql("""
                UPDATE shipping_methods
                SET "FreeAboveAmount" = source.amount
                FROM (
                    SELECT CAST("Value" AS bigint) AS amount
                    FROM settings
                    WHERE "Section" = 'store'
                      AND "Key" = 'freeShippingThreshold'
                      AND "Value" ~ '^[0-9]+$'
                      AND CAST("Value" AS bigint) > 0
                    LIMIT 1
                ) AS source;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FreeAboveAmount",
                table: "shipping_methods");
        }
    }
}
