using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorShoppingAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "CustomerId",
                table: "admin_users",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_CustomerId",
                table: "admin_users",
                column: "CustomerId",
                unique: true,
                filter: "\"CustomerId\" IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "FK_admin_users_customers_CustomerId",
                table: "admin_users",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_admin_users_customers_CustomerId",
                table: "admin_users");

            migrationBuilder.DropIndex(
                name: "IX_admin_users_CustomerId",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "CustomerId",
                table: "admin_users");
        }
    }
}
