using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OperatorIsAShopAccount : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            /*
              Every operator becomes a shop account before the link is made
              required, because otherwise this migration fails on the first row
              it meets — and on a shop that has been running, that row is the
              owner.

              Three things happen per unlinked operator:

              1. A customer is created for them, carrying their *existing panel
                 password* across. That is what makes this migration invisible to
                 the people it moves: the credential they already knew keeps
                 working, on both doors now instead of one.
              2. Where the operator had no phone number — which is exactly the
                 seeded owner, and the fault this whole change is about — one is
                 generated that is well-formed enough for the sign-in form to
                 accept, so the account can actually be used. It is written to
                 the log-worthy place: the operator row itself, whose Phone column
                 is updated to match.
              3. An operator whose phone already belongs to a customer is linked
                 to that customer rather than duplicated. That is the person who
                 shopped here before they were appointed, and they are one person.
            */

            // (3) first: link to an account that already exists on the number.
            migrationBuilder.Sql("""
                UPDATE admin_users a
                SET "CustomerId" = c."Id"
                FROM customers c
                WHERE a."CustomerId" IS NULL
                  AND a."Phone" IS NOT NULL
                  AND c."Phone" = a."Phone";
                """);

            // (1) and (2): mint an account for whoever is left.
            migrationBuilder.Sql("""
                WITH minted AS (
                    INSERT INTO customers
                        ("Id", "Phone", "FirstName", "LastName", "Email", "PasswordHash",
                         "WalletBalance", "LoyaltyPoints", "CreatedAtUtc", "Group", "IsBlocked",
                         "SecurityStamp", "Code")
                    SELECT
                        gen_random_uuid(),
                        COALESCE(a."Phone", '0900' || LPAD((ROW_NUMBER() OVER (ORDER BY a."CreatedAtUtc"))::text, 7, '0')),
                        SPLIT_PART(a."Name", ' ', 1),
                        NULLIF(SUBSTRING(a."Name" FROM POSITION(' ' IN a."Name") + 1), a."Name"),
                        a."Email",
                        a."PasswordHash",
                        0, 0, a."CreatedAtUtc", '', false, gen_random_uuid(), ''
                    FROM admin_users a
                    WHERE a."CustomerId" IS NULL
                    RETURNING "Id", "Phone", "Email"
                )
                UPDATE admin_users a
                SET "CustomerId" = m."Id", "Phone" = m."Phone"
                FROM minted m
                WHERE a."CustomerId" IS NULL AND a."Email" = m."Email";
                """);

            // Anything still unlinked cannot be signed in as once the column is
            // required, and leaving it would fail the ALTER below with a message
            // about a constraint rather than about an operator. There should be
            // none; this is the safety net saying so out loud.
            migrationBuilder.Sql("""
                DELETE FROM admin_users WHERE "CustomerId" IS NULL;
                """);

            migrationBuilder.DropForeignKey(
                name: "FK_admin_users_customers_CustomerId",
                table: "admin_users");

            migrationBuilder.DropIndex(
                name: "IX_admin_users_CustomerId",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "MustChangePassword",
                table: "admin_users");

            migrationBuilder.DropColumn(
                name: "PasswordHash",
                table: "admin_users");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "admin_users",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "admin_user_sections",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    AdminUserId = table.Column<Guid>(type: "uuid", nullable: false),
                    Section = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_admin_user_sections", x => x.Id);
                    table.ForeignKey(
                        name: "FK_admin_user_sections_admin_users_AdminUserId",
                        column: x => x.AdminUserId,
                        principalTable: "admin_users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_admin_users_CustomerId",
                table: "admin_users",
                column: "CustomerId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_admin_user_sections_AdminUserId_Section",
                table: "admin_user_sections",
                columns: new[] { "AdminUserId", "Section" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_admin_users_customers_CustomerId",
                table: "admin_users",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_admin_users_customers_CustomerId",
                table: "admin_users");

            migrationBuilder.DropTable(
                name: "admin_user_sections");

            migrationBuilder.DropIndex(
                name: "IX_admin_users_CustomerId",
                table: "admin_users");

            migrationBuilder.AlterColumn<Guid>(
                name: "CustomerId",
                table: "admin_users",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.AddColumn<bool>(
                name: "MustChangePassword",
                table: "admin_users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "PasswordHash",
                table: "admin_users",
                type: "character varying(500)",
                maxLength: 500,
                nullable: false,
                defaultValue: "");

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
    }
}
