using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderInvoiceNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "DeliveredAtUtc",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InvoiceNumber",
                table: "orders",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_orders_InvoiceNumber",
                table: "orders",
                column: "InvoiceNumber",
                unique: true,
                filter: "\"InvoiceNumber\" IS NOT NULL");

            // Orders already delivered before this migration ran. Without this
            // they would sit in a permanent hole: delivered is a terminal
            // status, so they can never transition again, and the transition is
            // what issues the number — every one of them would be un-invoiceable
            // forever.
            //
            // The number is drawn the same way Order.TransitionTo draws it (16
            // random digits, uniqueness owned by the index above) and the
            // retry-on-conflict loop is what makes a collision a re-draw rather
            // than a failed migration. Nothing is invented: an invoice number is
            // arbitrary by design, so minting one late is the same act as
            // minting it on time.
            //
            // The delivery date, unlike the number, is a fact about the past
            // that this table never recorded — so it is read from the timeline
            // row that moved the order to Delivered, and only falls back to the
            // placement date when even that is missing.
            migrationBuilder.Sql(
                """
                UPDATE orders o
                SET "DeliveredAtUtc" = COALESCE((
                        SELECT MIN(e."AtUtc") FROM order_timeline_events e
                        WHERE e."OrderId" = o."Id" AND e."Status" = 'Delivered'
                    ), o."PlacedAtUtc")
                WHERE o."Status" = 'Delivered' AND o."DeliveredAtUtc" IS NULL;

                DO $$
                BEGIN
                    LOOP
                        BEGIN
                            UPDATE orders
                            SET "InvoiceNumber" = LPAD(
                                (FLOOR(RANDOM() * 10000000000000000)::NUMERIC(17,0))::TEXT, 16, '0')
                            WHERE "Status" = 'Delivered' AND "InvoiceNumber" IS NULL;
                            EXIT;
                        EXCEPTION WHEN unique_violation THEN
                            -- Two rows drew the same number. Try the lot again.
                        END;
                    END LOOP;
                END $$;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_InvoiceNumber",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "DeliveredAtUtc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "InvoiceNumber",
                table: "orders");
        }
    }
}
