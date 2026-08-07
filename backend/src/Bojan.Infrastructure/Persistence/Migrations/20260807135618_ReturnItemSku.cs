using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ReturnItemSku : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SkuId",
                table: "return_items",
                type: "uuid",
                nullable: true);

            // Existing rows keep null, which is correct rather than merely
            // convenient: a return filed before this column existed named a
            // product and nothing else, and guessing which variant it meant
            // would be inventing a fact. Null is exactly what the old data
            // knew — "the product itself" — and it is also what a product with
            // no variants will always carry.
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "SkuId",
                table: "return_items");
        }
    }
}
