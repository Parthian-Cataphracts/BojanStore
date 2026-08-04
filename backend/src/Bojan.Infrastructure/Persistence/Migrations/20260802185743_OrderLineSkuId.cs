using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderLineSkuId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "SkuId",
                table: "order_lines",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_order_lines_SkuId",
                table: "order_lines",
                column: "SkuId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_order_lines_SkuId",
                table: "order_lines");

            migrationBuilder.DropColumn(
                name: "SkuId",
                table: "order_lines");
        }
    }
}
