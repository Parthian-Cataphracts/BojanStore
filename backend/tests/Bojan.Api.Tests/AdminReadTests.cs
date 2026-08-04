using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Common;
using Bojan.Domain.Orders;

namespace Bojan.Api.Tests;

/// <summary>
/// Phase 6 — the panel's lists and its aggregates.
/// </summary>
/// <remarks>
/// The aggregate tests matter more than they look: they are what proves the
/// grouped queries translate at all. <c>BACKEND.md</c> Phase 6 asks for the
/// sums to run in SQL, and a <c>GroupBy</c> the provider cannot translate does
/// not fall back quietly — it throws, which is exactly what these would catch.
/// </remarks>
public sealed class AdminReadTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 200_000, stock: 10);
            var lowStock = await TestData.AddProductAsync(db, brandId, categoryId, "p-low", 50_000, stock: 2);

            product.CostPrice = new Money(120_000);
            lowStock.CostPrice = new Money(30_000);

            var customer = await TestData.AddCustomerAsync(db, "09121110040");
            var address = await TestData.AddAddressAsync(db, customer.Id);
            _customerId = customer.Id;

            // Two orders: one delivered, one cancelled. Only the first counts
            // as revenue.
            db.Orders.Add(MakeOrder(customer.Id, address.Id, product, OrderStatus.Delivered));
            db.Orders.Add(MakeOrder(customer.Id, address.Id, product, OrderStatus.Cancelled));

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
            await db.SaveChangesAsync();
        });

        _client = _factory.CreateAdminClient(ownerId);
    }

    private static Order MakeOrder(Guid customerId, Guid addressId, Domain.Catalogue.Product product, OrderStatus status)
    {
        var order = Order.Create(
            OrderNumber.NewOrderNumber(),
            customerId,
            [new OrderLineDraft(product.Id, product.Slug, product.Title, product.ImageUrl, 2, product.Price)],
            addressId,
            "تهران، خیابان آزمایشی",
            "ارسال استاندارد",
            "پرداخت در محل",
            product.Price * 2,
            Money.Zero,
            new Money(45_000),
            Guid.NewGuid().ToString());

        if (status != OrderStatus.Pending)
        {
            order.TransitionTo(status);
        }

        return order;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task The_order_list_is_paged_and_carries_the_customer()
    {
        var body = await (await _client.GetAsync("/api/admin/orders")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(2, body.GetProperty("total").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());

        var order = body.GetProperty("items")[0];
        Assert.Equal("آزمون کاربر", order.GetProperty("customer").GetString());
        Assert.Equal("09121110040", order.GetProperty("customerPhone").GetString());
    }

    [Fact]
    public async Task The_admin_product_list_carries_the_cost_price()
    {
        var body = await (await _client.GetAsync("/api/admin/products")).Content.ReadFromJsonAsync<JsonElement>();

        var product = body.GetProperty("items").EnumerateArray().First(p => p.GetProperty("sku").GetString() == "BZ-P-01");

        // The one field the storefront must never see, on the one API that may.
        Assert.Equal(120_000, product.GetProperty("costPrice").GetInt64());
        Assert.Equal("published", product.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_customer_list_counts_orders_and_lifetime_spend()
    {
        var body = await (await _client.GetAsync("/api/admin/customers")).Content.ReadFromJsonAsync<JsonElement>();

        var customer = body.GetProperty("items")[0];
        // Cancelled orders count for neither.
        Assert.Equal(1, customer.GetProperty("orderCount").GetInt32());
        Assert.Equal(445_000, customer.GetProperty("totalSpent").GetInt64());
        Assert.Equal("active", customer.GetProperty("status").GetString());
    }

    [Fact]
    public async Task The_dashboard_aggregates_run_and_exclude_cancelled_orders()
    {
        var body = await (await _client.GetAsync("/api/admin/dashboard")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("ordersThisMonth").GetInt32());
        Assert.Equal(445_000, body.GetProperty("revenueThisMonth").GetInt64());
        Assert.Equal(1, body.GetProperty("newCustomersThisMonth").GetInt32());
        // p-low sits at 2, under the low-stock threshold.
        Assert.Equal(1, body.GetProperty("lowStockProducts").GetInt32());
    }

    [Fact]
    public async Task The_sales_series_groups_by_period()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/sales?grouping=day"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(445_000, body[0].GetProperty("revenue").GetInt64());
        Assert.Equal(1, body[0].GetProperty("orders").GetInt32());

        var monthly = await (await _client.GetAsync("/api/admin/reports/sales?grouping=month"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, monthly.GetArrayLength());
    }

    [Fact]
    public async Task Order_status_counts_cover_every_status_present()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/order-status"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var counts = body.EnumerateArray()
            .ToDictionary(row => row.GetProperty("status").GetString()!, row => row.GetProperty("count").GetInt32());

        Assert.Equal(1, counts["delivered"]);
        Assert.Equal(1, counts["cancelled"]);
    }

    [Fact]
    public async Task Top_products_rank_by_units_sold()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/top-products"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(2, body[0].GetProperty("unitsSold").GetInt32());
        Assert.Equal(400_000, body[0].GetProperty("revenue").GetInt64());
    }

    [Fact]
    public async Task Stock_levels_value_the_inventory_at_cost()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/stock-levels"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetProperty("inStock").GetInt32());
        Assert.Equal(1, body.GetProperty("lowStock").GetInt32());
        // 10 x 120,000 + 2 x 30,000.
        Assert.Equal(1_260_000, body.GetProperty("inventoryValue").GetInt64());
    }

    [Fact]
    public async Task Financial_totals_subtract_the_cost_of_goods()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/financial"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(400_000, body.GetProperty("grossRevenue").GetInt64());
        Assert.Equal(45_000, body.GetProperty("shipping").GetInt64());
        Assert.Equal(445_000, body.GetProperty("netRevenue").GetInt64());
        // Two units at a cost of 120,000.
        Assert.Equal(240_000, body.GetProperty("costOfGoods").GetInt64());
        Assert.Equal(160_000, body.GetProperty("grossProfit").GetInt64());
    }

    /// <summary>
    /// The catalogue count is the catalogue's, not a page's.
    /// </summary>
    /// <remarks>
    /// Screen 135 read `products.length` off a page capped at 200 and printed
    /// it as "تعداد محصول", so a larger catalogue reported exactly 200. Archived
    /// products are counted too — the report has a column for them, and the
    /// default query filter would otherwise hide them from their own total.
    /// </remarks>
    [Fact]
    public async Task The_catalogue_summary_counts_every_product_including_archived()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/catalogue-summary"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var total = body.GetProperty("total").GetInt32();
        var published = body.GetProperty("published").GetInt32();
        var draft = body.GetProperty("draft").GetInt32();
        var archived = body.GetProperty("archived").GetInt32();

        Assert.True(total > 0);
        // The three states partition the catalogue; anything else means one of
        // them is being counted twice or missed.
        Assert.Equal(total, published + draft + archived);
    }

    [Fact]
    public async Task The_customer_summary_counts_the_whole_base()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/customer-summary"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.True(body.GetProperty("total").GetInt32() > 0);

        // Lifetime spend is the same money the financial report reports, taken
        // over the same orders.
        var financial = await (await _client.GetAsync("/api/admin/reports/financial"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(
            financial.GetProperty("netRevenue").GetInt64(),
            body.GetProperty("totalSpend").GetInt64());
    }

    /// <summary>Units on hand, so the inventory report stops summing a page.</summary>
    [Fact]
    public async Task Stock_levels_report_the_units_on_hand()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/stock-levels"))
            .Content.ReadFromJsonAsync<JsonElement>();

        // The fixture is two products, stocked 10 and 2.
        Assert.Equal(12, body.GetProperty("totalUnits").GetInt32());
    }

    /// <summary>
    /// The payment-method split covers the same orders the totals do.
    /// </summary>
    /// <remarks>
    /// The panel used to build this table itself by filtering a capped page of
    /// orders against two hard-coded method names. Cash on delivery is a third,
    /// so those orders were missing from the table while still counted in the
    /// net revenue printed above it. Summing the split has to give the net back.
    /// </remarks>
    [Fact]
    public async Task The_payment_method_split_adds_up_to_net_revenue()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/financial"))
            .Content.ReadFromJsonAsync<JsonElement>();

        var rows = body.GetProperty("byPaymentMethod").EnumerateArray().ToList();

        Assert.NotEmpty(rows);
        Assert.Equal(
            body.GetProperty("netRevenue").GetInt64(),
            rows.Sum(row => row.GetProperty("amount").GetInt64()));
        Assert.Equal(
            body.GetProperty("orderCount").GetInt32(),
            rows.Sum(row => row.GetProperty("count").GetInt32()));
    }

    [Fact]
    public async Task Customer_growth_runs()
    {
        var body = await (await _client.GetAsync("/api/admin/reports/customer-growth"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(1, body.GetArrayLength());
        Assert.Equal(1, body[0].GetProperty("newCustomers").GetInt32());
    }

    [Fact]
    public async Task An_order_status_change_notifies_the_customer_and_is_audited()
    {
        string orderId = null!;

        var orders = await (await _client.GetAsync("/api/admin/orders?status=pending"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, orders.GetProperty("total").GetInt32());

        await _factory.WithDbAsync(async db =>
        {
            var order = Order.Create(
                OrderNumber.NewOrderNumber(),
                _customerId,
                [new OrderLineDraft(Guid.NewGuid(), "p-01", "محصول", "https://example.test/p.jpg", 1, new Money(100_000))],
                Guid.NewGuid(),
                "تهران",
                "ارسال استاندارد",
                "پرداخت در محل",
                new Money(100_000),
                Money.Zero,
                Money.Zero,
                Guid.NewGuid().ToString());

            db.Orders.Add(order);
            await db.SaveChangesAsync();
            orderId = order.Id.ToString();
        });

        var response = await _client.PostAsJsonAsync("/api/admin/orders/status", new
        {
            id = orderId,
            status = "shipped",
            trackingCode = "TRK-0001",
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var order = db.Orders.Single(o => o.Id == Guid.Parse(orderId));
            Assert.Equal(OrderStatus.Shipped, order.Status);
            Assert.Equal("TRK-0001", order.TrackingCode);

            Assert.Contains(db.CustomerNotifications, n => n.CustomerId == _customerId);
            Assert.Contains(db.AuditEntries, a => a.Action == "order.status.changed");
        });
    }
}
