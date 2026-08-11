using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Domain.Business;
using Bojan.Domain.Catalogue;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Turning a business request into a priced pro-forma.
/// </summary>
/// <remarks>
/// The half of B2B that did not exist: organisations could ask and operators
/// could move a request through its statuses, but nothing could answer with a
/// figure. The panel's "issue a quote" button posted <c>status: quoted</c>,
/// which told the organisation a pro-forma existed and produced none.
///
/// What these pin down is where the money comes from. The operator names
/// products and quantities; every unit price is read from the catalogue and
/// every discount from the product's own volume ladder, so a crafted body
/// cannot quote a price the shop never set.
/// </remarks>
public sealed class BusinessQuoteTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _productId;
    private Guid _requestId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid adminId = default;

        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            var product = await TestData.AddProductAsync(db, brandId, categoryId, "q-01", 50_000, stock: 10_000);
            var admin = await TestData.AddAdminAsync(db, AdminRole.Sales, "sales@example.com");

            // 20+ units at 10%, 100+ at 18% — the ladder every assertion below
            // reads against.
            db.ProductVolumeTiers.AddRange(
                new ProductVolumeTier { ProductId = product.Id, MinimumQuantity = 20, DiscountPercent = 10 },
                new ProductVolumeTier { ProductId = product.Id, MinimumQuantity = 100, DiscountPercent = 18 });

            var request = BusinessRequest.Create(
                "B2B-0001",
                BusinessRequestKind.Bulk,
                "استعلام قیمت",
                "شرکت آزمون",
                "کارشناس خرید",
                "09120000000",
                DateTimeOffset.UtcNow,
                email: "buyer@example.test",
                itemCount: 120);

            db.BusinessRequests.Add(request);
            await db.SaveChangesAsync();

            _productId = product.Id;
            _requestId = request.Id;
            adminId = admin.Id;
        });

        _client = _factory.CreateAdminClient(adminId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> IssueAsync(object body) =>
        _client.PostAsJsonAsync("/api/admin/business-requests/quote", body);

    [Fact]
    public async Task The_ladder_prices_the_line_rather_than_the_request()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 120 } },
            taxRatePercent = 0,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var quote = await db.Quotes.Include(q => q.Lines).SingleAsync();
            var line = Assert.Single(quote.Lines);

            // 120 units reaches the 18% rung, not the 10% one below it.
            Assert.Equal(41_000, line.UnitPrice.Amount);
            Assert.Equal(120, line.Quantity);
            Assert.Equal(4_920_000, quote.Subtotal.Amount);
        });
    }

    /// <summary>
    /// The request moves with the quote — this is the status change the old
    /// button made on its own, now made only when a document actually exists.
    /// </summary>
    [Fact]
    public async Task Issuing_a_quote_moves_the_request_to_quoted()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 5 } },
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var request = await db.BusinessRequests.SingleAsync();
            Assert.Equal(BusinessRequestStatus.Quoted, request.Status);
        });
    }

    /// <summary>
    /// A rep may price a line by hand. It is stored as the unit price rather
    /// than as a second discount, so the document says what will be charged and
    /// nothing has to be recomputed to read it.
    /// </summary>
    [Fact]
    public async Task A_negotiated_line_overrides_the_ladder()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 120, unitPriceOverride = 30_000 } },
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var line = await db.QuoteLines.SingleAsync();
            Assert.Equal(30_000, line.UnitPrice.Amount);
        });
    }

    /// <summary>
    /// The discount comes off the lines first and tax is charged on what is
    /// left, which is the order the document totals in.
    /// </summary>
    [Fact]
    public async Task Tax_is_charged_on_the_discounted_subtotal()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 20 } },
            discount = 100_000,
            taxRatePercent = 10,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var quote = await db.Quotes.Include(q => q.Lines).SingleAsync();

            // 20 units at the 10% rung: 45,000 each, 900,000 the lot.
            Assert.Equal(900_000, quote.Subtotal.Amount);
            Assert.Equal(80_000, quote.Tax.Amount);
            Assert.Equal(880_000, quote.Total.Amount);
        });
    }

    /// <summary>
    /// A quote that silently drops a line is a quote for something other than
    /// what was asked for.
    /// </summary>
    [Fact]
    public async Task A_line_naming_a_product_that_does_not_exist_is_refused()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = Guid.NewGuid(), quantity = 5 } },
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.False(await db.Quotes.AnyAsync()));
    }

    [Fact]
    public async Task A_quote_with_no_lines_is_refused()
    {
        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = Array.Empty<object>(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// A rejected request is finished. Pricing one would be answering an enquiry
    /// the shop has already declined, and the aggregate refuses the transition —
    /// what matters here is that the refusal does not leave a quote behind.
    /// </summary>
    [Fact]
    public async Task A_rejected_request_cannot_be_quoted()
    {
        // Rejected the way an operator does it, rather than by writing the
        // status straight into the row: the transition appends a timeline entry
        // the repository files, and a test that skips that is testing a state
        // the application never produces.
        var rejection = await _client.PostAsJsonAsync("/api/admin/business-requests", new
        {
            id = _requestId.ToString(),
            status = "rejected",
        });

        rejection.EnsureSuccessStatusCode();

        var response = await IssueAsync(new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 5 } },
        });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.False(await db.Quotes.AnyAsync()));
    }

    /// <summary>
    /// The catalogue the composer offers. Its ladder travels with the price so
    /// the screen can show a rep what a hundred units come to before they issue
    /// anything.
    /// </summary>
    [Fact]
    public async Task The_quotable_catalogue_carries_each_products_ladder()
    {
        var read = await _client.GetFromJsonAsync<JsonElement>("/api/admin/business-requests/products");

        var product = read.EnumerateArray()
            .Single(item => item.GetProperty("id").GetString() == _productId.ToString());

        Assert.Equal(50_000, product.GetProperty("price").GetInt64());

        var tiers = product.GetProperty("tiers").EnumerateArray().ToList();
        Assert.Equal(2, tiers.Count);
        Assert.Equal(20, tiers[0].GetProperty("minimumQuantity").GetInt32());
        Assert.Equal(18, tiers[1].GetProperty("discountPercent").GetInt32());
    }

    /// <summary>
    /// Sales, not the catalogue role: a rep issuing a quote has to pick products
    /// and is not thereby trusted to edit them. Support holds neither.
    /// </summary>
    [Fact]
    public async Task Support_cannot_issue_a_quote()
    {
        Guid supportId = default;
        await _factory.WithDbAsync(async db =>
        {
            var support = await TestData.AddAdminAsync(db, AdminRole.Support, "support@example.com");
            supportId = support.Id;
        });

        using var support = _factory.CreateAdminClient(supportId);

        var response = await support.PostAsJsonAsync("/api/admin/business-requests/quote", new
        {
            requestId = _requestId.ToString(),
            lines = new[] { new { productId = _productId, quantity = 5 } },
        });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
