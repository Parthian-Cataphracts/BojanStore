using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class CustomerCode : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Code",
                table: "customers",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                defaultValue: "");

            /*
                Every existing customer gets a code before the unique index goes
                on, because the column was just added with the same default for
                all of them — and a unique index over a table of empty strings
                fails the moment there are two customers. On an empty database
                this does nothing; on a running shop it is the difference
                between a migration and an outage.

                Numbered by registration date, so the oldest customer is
                BZ-00001 and the codes read in the order the shop acquired them.
            */
            migrationBuilder.Sql("""
                WITH numbered AS (
                    SELECT "Id", ROW_NUMBER() OVER (ORDER BY "CreatedAtUtc", "Id") AS seq
                    FROM customers
                )
                UPDATE customers
                SET "Code" = 'BZ-' || LPAD(numbered.seq::text, 5, '0')
                FROM numbered
                WHERE customers."Id" = numbered."Id";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_customers_Code",
                table: "customers",
                column: "Code",
                unique: true);

            /*
                A sequence, not a `MAX(...) + 1`.

                Two registrations landing in the same instant would both read the
                same maximum and both build the same code, and the unique index
                above would fail one of them — turning a race into a customer
                who could not register. `nextval` hands out a distinct number per
                caller with no lock held and no retry to write, which is the
                whole reason sequences exist.

                It starts past the codes the backfill just issued, so a shop with
                existing customers carries on from where they end.
            */
            migrationBuilder.Sql("""
                CREATE SEQUENCE IF NOT EXISTS customer_code_seq AS bigint START WITH 1;
                SELECT setval('customer_code_seq', GREATEST((SELECT COUNT(*) FROM customers), 1));
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP SEQUENCE IF EXISTS customer_code_seq;");

            migrationBuilder.DropIndex(
                name: "IX_customers_Code",
                table: "customers");

            migrationBuilder.DropColumn(
                name: "Code",
                table: "customers");
        }
    }
}
