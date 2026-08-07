using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Bojan.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Turns on referential integrity across the schema.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fifty-eight tables held thirteen foreign keys between them, all of them
    /// on collections EF wires up on its own. Every other reference — an order
    /// naming its customer, a review naming its product, an audit row naming
    /// who acted — was an unconstrained uuid column, so a bug that wrote the
    /// wrong id produced a row that looked fine and read as nothing.
    /// </para>
    /// <para>
    /// This will refuse to apply to a database that already holds an orphaned
    /// row, which is the point: those rows are the damage the missing
    /// constraints allowed, and they have to be looked at rather than carried
    /// forward silently.
    /// </para>
    /// </remarks>
    public partial class ReferentialIntegrity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_wishlist_items_ProductId",
                table: "wishlist_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_top_ups_ReviewedByAdminId",
                table: "wallet_top_ups",
                column: "ReviewedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_wallet_top_ups_WalletTransactionId",
                table: "wallet_top_ups",
                column: "WalletTransactionId");

            migrationBuilder.CreateIndex(
                name: "IX_support_tickets_AssigneeId",
                table: "support_tickets",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_settings_UpdatedById",
                table: "settings",
                column: "UpdatedById");

            migrationBuilder.CreateIndex(
                name: "IX_return_requests_DecidedById",
                table: "return_requests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_return_items_ProductId",
                table: "return_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_report_exports_RequestedById",
                table: "report_exports",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_recently_viewed_items_ProductId",
                table: "recently_viewed_items",
                column: "ProductId");

            migrationBuilder.CreateIndex(
                name: "IX_product_questions_CustomerId",
                table: "product_questions",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_notification_campaigns_ActorId",
                table: "notification_campaigns",
                column: "ActorId");

            migrationBuilder.CreateIndex(
                name: "IX_live_chat_messages_CustomerId",
                table: "live_chat_messages",
                column: "CustomerId");

            migrationBuilder.CreateIndex(
                name: "IX_coupon_grants_CouponId",
                table: "coupon_grants",
                column: "CouponId");

            migrationBuilder.CreateIndex(
                name: "IX_categories_ParentId",
                table: "categories",
                column: "ParentId");

            migrationBuilder.CreateIndex(
                name: "IX_business_requests_AssigneeId",
                table: "business_requests",
                column: "AssigneeId");

            migrationBuilder.CreateIndex(
                name: "IX_backup_jobs_RequestedById",
                table: "backup_jobs",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_api_keys_CreatedById",
                table: "api_keys",
                column: "CreatedById");

            migrationBuilder.AddForeignKey(
                name: "FK_api_keys_admin_users_CreatedById",
                table: "api_keys",
                column: "CreatedById",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_audit_entries_admin_users_ActorId",
                table: "audit_entries",
                column: "ActorId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_backup_jobs_admin_users_RequestedById",
                table: "backup_jobs",
                column: "RequestedById",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_organizations_customers_CustomerId",
                table: "business_organizations",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_business_requests_admin_users_AssigneeId",
                table: "business_requests",
                column: "AssigneeId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_business_requests_customers_CustomerId",
                table: "business_requests",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_categories_categories_ParentId",
                table: "categories",
                column: "ParentId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_collection_products_products_ProductId",
                table: "collection_products",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_coupon_grants_coupons_CouponId",
                table: "coupon_grants",
                column: "CouponId",
                principalTable: "coupons",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_coupon_grants_customers_CustomerId",
                table: "coupon_grants",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_customer_notifications_customers_CustomerId",
                table: "customer_notifications",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_live_chat_messages_customers_CustomerId",
                table: "live_chat_messages",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_notification_campaigns_admin_users_ActorId",
                table: "notification_campaigns",
                column: "ActorId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_order_lines_product_skus_SkuId",
                table: "order_lines",
                column: "SkuId",
                principalTable: "product_skus",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_order_lines_products_ProductId",
                table: "order_lines",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_orders_customers_CustomerId",
                table: "orders",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_password_reset_tokens_customers_CustomerId",
                table: "password_reset_tokens",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_attributes_products_ProductId",
                table: "product_attributes",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_questions_customers_CustomerId",
                table: "product_questions",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_questions_products_ProductId",
                table: "product_questions",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_reviews_customers_CustomerId",
                table: "product_reviews",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_reviews_products_ProductId",
                table: "product_reviews",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_skus_products_ProductId",
                table: "product_skus",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_product_variant_axes_products_ProductId",
                table: "product_variant_axes",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_products_brands_BrandId",
                table: "products",
                column: "BrandId",
                principalTable: "brands",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_products_categories_CategoryId",
                table: "products",
                column: "CategoryId",
                principalTable: "categories",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_quotes_business_requests_BusinessRequestId",
                table: "quotes",
                column: "BusinessRequestId",
                principalTable: "business_requests",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recently_viewed_items_customers_CustomerId",
                table: "recently_viewed_items",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_recently_viewed_items_products_ProductId",
                table: "recently_viewed_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_report_exports_admin_users_RequestedById",
                table: "report_exports",
                column: "RequestedById",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_items_products_ProductId",
                table: "return_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_admin_users_DecidedById",
                table: "return_requests",
                column: "DecidedById",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_customers_CustomerId",
                table: "return_requests",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_return_requests_orders_OrderId",
                table: "return_requests",
                column: "OrderId",
                principalTable: "orders",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_search_history_entries_customers_CustomerId",
                table: "search_history_entries",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_settings_admin_users_UpdatedById",
                table: "settings",
                column: "UpdatedById",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_alerts_products_ProductId",
                table: "stock_alerts",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_stock_movements_products_ProductId",
                table: "stock_movements",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_support_tickets_admin_users_AssigneeId",
                table: "support_tickets",
                column: "AssigneeId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_support_tickets_customers_CustomerId",
                table: "support_tickets",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wallet_top_ups_admin_users_ReviewedByAdminId",
                table: "wallet_top_ups",
                column: "ReviewedByAdminId",
                principalTable: "admin_users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wallet_top_ups_customers_CustomerId",
                table: "wallet_top_ups",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_wallet_top_ups_wallet_transactions_WalletTransactionId",
                table: "wallet_top_ups",
                column: "WalletTransactionId",
                principalTable: "wallet_transactions",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_wallet_transactions_customers_CustomerId",
                table: "wallet_transactions",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_items_customers_CustomerId",
                table: "wishlist_items",
                column: "CustomerId",
                principalTable: "customers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_wishlist_items_products_ProductId",
                table: "wishlist_items",
                column: "ProductId",
                principalTable: "products",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_api_keys_admin_users_CreatedById",
                table: "api_keys");

            migrationBuilder.DropForeignKey(
                name: "FK_audit_entries_admin_users_ActorId",
                table: "audit_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_backup_jobs_admin_users_RequestedById",
                table: "backup_jobs");

            migrationBuilder.DropForeignKey(
                name: "FK_business_organizations_customers_CustomerId",
                table: "business_organizations");

            migrationBuilder.DropForeignKey(
                name: "FK_business_requests_admin_users_AssigneeId",
                table: "business_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_business_requests_customers_CustomerId",
                table: "business_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_categories_categories_ParentId",
                table: "categories");

            migrationBuilder.DropForeignKey(
                name: "FK_collection_products_products_ProductId",
                table: "collection_products");

            migrationBuilder.DropForeignKey(
                name: "FK_coupon_grants_coupons_CouponId",
                table: "coupon_grants");

            migrationBuilder.DropForeignKey(
                name: "FK_coupon_grants_customers_CustomerId",
                table: "coupon_grants");

            migrationBuilder.DropForeignKey(
                name: "FK_customer_notifications_customers_CustomerId",
                table: "customer_notifications");

            migrationBuilder.DropForeignKey(
                name: "FK_live_chat_messages_customers_CustomerId",
                table: "live_chat_messages");

            migrationBuilder.DropForeignKey(
                name: "FK_notification_campaigns_admin_users_ActorId",
                table: "notification_campaigns");

            migrationBuilder.DropForeignKey(
                name: "FK_order_lines_product_skus_SkuId",
                table: "order_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_order_lines_products_ProductId",
                table: "order_lines");

            migrationBuilder.DropForeignKey(
                name: "FK_orders_customers_CustomerId",
                table: "orders");

            migrationBuilder.DropForeignKey(
                name: "FK_password_reset_tokens_customers_CustomerId",
                table: "password_reset_tokens");

            migrationBuilder.DropForeignKey(
                name: "FK_product_attributes_products_ProductId",
                table: "product_attributes");

            migrationBuilder.DropForeignKey(
                name: "FK_product_questions_customers_CustomerId",
                table: "product_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_questions_products_ProductId",
                table: "product_questions");

            migrationBuilder.DropForeignKey(
                name: "FK_product_reviews_customers_CustomerId",
                table: "product_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_product_reviews_products_ProductId",
                table: "product_reviews");

            migrationBuilder.DropForeignKey(
                name: "FK_product_skus_products_ProductId",
                table: "product_skus");

            migrationBuilder.DropForeignKey(
                name: "FK_product_variant_axes_products_ProductId",
                table: "product_variant_axes");

            migrationBuilder.DropForeignKey(
                name: "FK_products_brands_BrandId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_products_categories_CategoryId",
                table: "products");

            migrationBuilder.DropForeignKey(
                name: "FK_quotes_business_requests_BusinessRequestId",
                table: "quotes");

            migrationBuilder.DropForeignKey(
                name: "FK_recently_viewed_items_customers_CustomerId",
                table: "recently_viewed_items");

            migrationBuilder.DropForeignKey(
                name: "FK_recently_viewed_items_products_ProductId",
                table: "recently_viewed_items");

            migrationBuilder.DropForeignKey(
                name: "FK_report_exports_admin_users_RequestedById",
                table: "report_exports");

            migrationBuilder.DropForeignKey(
                name: "FK_return_items_products_ProductId",
                table: "return_items");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_admin_users_DecidedById",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_customers_CustomerId",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_return_requests_orders_OrderId",
                table: "return_requests");

            migrationBuilder.DropForeignKey(
                name: "FK_search_history_entries_customers_CustomerId",
                table: "search_history_entries");

            migrationBuilder.DropForeignKey(
                name: "FK_settings_admin_users_UpdatedById",
                table: "settings");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_alerts_products_ProductId",
                table: "stock_alerts");

            migrationBuilder.DropForeignKey(
                name: "FK_stock_movements_products_ProductId",
                table: "stock_movements");

            migrationBuilder.DropForeignKey(
                name: "FK_support_tickets_admin_users_AssigneeId",
                table: "support_tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_support_tickets_customers_CustomerId",
                table: "support_tickets");

            migrationBuilder.DropForeignKey(
                name: "FK_wallet_top_ups_admin_users_ReviewedByAdminId",
                table: "wallet_top_ups");

            migrationBuilder.DropForeignKey(
                name: "FK_wallet_top_ups_customers_CustomerId",
                table: "wallet_top_ups");

            migrationBuilder.DropForeignKey(
                name: "FK_wallet_top_ups_wallet_transactions_WalletTransactionId",
                table: "wallet_top_ups");

            migrationBuilder.DropForeignKey(
                name: "FK_wallet_transactions_customers_CustomerId",
                table: "wallet_transactions");

            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_items_customers_CustomerId",
                table: "wishlist_items");

            migrationBuilder.DropForeignKey(
                name: "FK_wishlist_items_products_ProductId",
                table: "wishlist_items");

            migrationBuilder.DropIndex(
                name: "IX_wishlist_items_ProductId",
                table: "wishlist_items");

            migrationBuilder.DropIndex(
                name: "IX_wallet_top_ups_ReviewedByAdminId",
                table: "wallet_top_ups");

            migrationBuilder.DropIndex(
                name: "IX_wallet_top_ups_WalletTransactionId",
                table: "wallet_top_ups");

            migrationBuilder.DropIndex(
                name: "IX_support_tickets_AssigneeId",
                table: "support_tickets");

            migrationBuilder.DropIndex(
                name: "IX_settings_UpdatedById",
                table: "settings");

            migrationBuilder.DropIndex(
                name: "IX_return_requests_DecidedById",
                table: "return_requests");

            migrationBuilder.DropIndex(
                name: "IX_return_items_ProductId",
                table: "return_items");

            migrationBuilder.DropIndex(
                name: "IX_report_exports_RequestedById",
                table: "report_exports");

            migrationBuilder.DropIndex(
                name: "IX_recently_viewed_items_ProductId",
                table: "recently_viewed_items");

            migrationBuilder.DropIndex(
                name: "IX_product_questions_CustomerId",
                table: "product_questions");

            migrationBuilder.DropIndex(
                name: "IX_notification_campaigns_ActorId",
                table: "notification_campaigns");

            migrationBuilder.DropIndex(
                name: "IX_live_chat_messages_CustomerId",
                table: "live_chat_messages");

            migrationBuilder.DropIndex(
                name: "IX_coupon_grants_CouponId",
                table: "coupon_grants");

            migrationBuilder.DropIndex(
                name: "IX_categories_ParentId",
                table: "categories");

            migrationBuilder.DropIndex(
                name: "IX_business_requests_AssigneeId",
                table: "business_requests");

            migrationBuilder.DropIndex(
                name: "IX_backup_jobs_RequestedById",
                table: "backup_jobs");

            migrationBuilder.DropIndex(
                name: "IX_api_keys_CreatedById",
                table: "api_keys");
        }
    }
}
