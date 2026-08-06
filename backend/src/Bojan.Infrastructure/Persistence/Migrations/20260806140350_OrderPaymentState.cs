using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class OrderPaymentState : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "PaidAtUtc",
                table: "orders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentMethodCode",
                table: "orders",
                type: "character varying(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PaymentReference",
                table: "orders",
                type: "character varying(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PaymentStatus",
                table: "orders",
                type: "character varying(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<Guid>(
                name: "SettledById",
                table: "orders",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ActorId",
                table: "order_timeline_events",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "FromStatus",
                table: "order_timeline_events",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Reason",
                table: "order_timeline_events",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            // Orders that already exist predate the idea of a payment state, and
            // the empty-string defaults above would leave every one of them with
            // a method code that matches nothing and a status that parses to
            // nothing. Two consequences on a live database: the panel cannot
            // read them back, and none of them could be moved past packing,
            // because an order with no recorded payment is not allowed to ship.
            //
            // The method code is recovered from the title, which is the only
            // record of the choice these rows carry. The seeded titles are the
            // three below; anything else is left as the gateway, which is the
            // strictest reading — it is the one method that must be settled
            // before the order can travel.
            migrationBuilder.Sql("""
                UPDATE orders
                SET "PaymentMethodCode" = CASE
                    WHEN "PaymentMethodName" LIKE '%محل%'    THEN 'cod'
                    WHEN "PaymentMethodName" LIKE '%کیف پول%' THEN 'wallet'
                    ELSE 'gateway'
                END
                WHERE "PaymentMethodCode" = '';
                """);

            // An order that already shipped or was delivered was settled by
            // whatever process this shop was running before the state existed;
            // refusing to acknowledge that would strand real fulfilled orders.
            // A cancelled or returned one that took money from the wallet had it
            // given back. Everything still in flight starts outstanding, which
            // is the honest answer: nothing recorded that it was paid.
            migrationBuilder.Sql("""
                UPDATE orders
                SET "PaymentStatus" = CASE
                    WHEN "Status" IN ('Shipped', 'Delivered')  THEN 'Paid'
                    WHEN "Status" IN ('Cancelled', 'Returned')
                         AND "WalletPaid" > 0                  THEN 'Refunded'
                    WHEN "WalletPaid" >= GREATEST("Subtotal" - "Discount", 0) + "Shipping"
                                                               THEN 'Paid'
                    ELSE 'AwaitingPayment'
                END,
                "PaidAtUtc" = CASE
                    WHEN "Status" IN ('Shipped', 'Delivered') THEN "PlacedAtUtc"
                    ELSE NULL
                END
                WHERE "PaymentStatus" = '';
                """);

            migrationBuilder.CreateIndex(
                name: "IX_orders_PaymentStatus_PlacedAtUtc",
                table: "orders",
                columns: new[] { "PaymentStatus", "PlacedAtUtc" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_orders_PaymentStatus_PlacedAtUtc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaidAtUtc",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentMethodCode",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentReference",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "PaymentStatus",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "SettledById",
                table: "orders");

            migrationBuilder.DropColumn(
                name: "ActorId",
                table: "order_timeline_events");

            migrationBuilder.DropColumn(
                name: "FromStatus",
                table: "order_timeline_events");

            migrationBuilder.DropColumn(
                name: "Reason",
                table: "order_timeline_events");
        }
    }
}
