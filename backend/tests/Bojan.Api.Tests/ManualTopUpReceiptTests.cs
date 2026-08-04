using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Common;
using Bojan.Domain.Customers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// The receipt on a card-to-card top-up — <c>POST /api/me/wallet/topup/manual</c>.
/// </summary>
/// <remarks>
/// <para>
/// The store requires one, and an operator opens it on the screen where they
/// decide whether to put money into a wallet. It was stored exactly as the
/// customer sent it: every other image a customer supplies is checked against
/// storage, this one was taken as given, so any URL here was a link the shop
/// asked its own staff to follow.
/// </para>
/// <para>
/// It also had nowhere legitimate to come from — <c>receipts</c> was not one of
/// the folders a customer may upload into — so satisfying the requirement
/// honestly was impossible. These cover both halves.
/// </para>
/// </remarks>
public sealed class ManualTopUpReceiptTests : IAsyncLifetime, IDisposable
{
    /// <summary>Card-to-card is off by default, so the flow has to be switched on to be tested.</summary>
    private sealed class ManualTopUpEnabledFactory : BojanApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["Wallet:ManualTopUpEnabled"] = "true",
                    ["Wallet:RequireReceipt"] = "true",
                }));
        }
    }

    private readonly ManualTopUpEnabledFactory _factory = new();
    private HttpClient _client = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, "09121230099");
            _customerId = customer.Id;
        });

        _client = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private object Body(string? receiptUrl) => new
    {
        amount = 250_000,
        trackingNumber = "123456789",
        paidOn = DateTime.UtcNow.ToString("yyyy-MM-dd"),
        receiptUrl,
        note = "کارت به کارت",
    };

    /// <summary>A URL this API issued into the receipts folder — the shape <c>LocalFileStorage</c> returns.</summary>
    private string OwnReceiptUrl()
    {
        using var scope = _factory.Services.CreateScope();
        var storage = scope.ServiceProvider.GetRequiredService<IFileStorage>();

        var url = $"/media/receipts/{Guid.NewGuid():n}.jpg";
        Assert.True(storage.IsOwnUrl(url, "receipts"), "The test's own URL must satisfy the check being tested.");
        return url;
    }

    [Theory]
    [InlineData("https://evil.example/receipt.png")]
    [InlineData("data:image/png;base64,iVBORw0KGgo=")]
    [InlineData("/media/avatars/00000000000000000000000000000000.jpg")]
    [InlineData("/media/receipts/../../etc/passwd")]
    public async Task A_receipt_this_api_did_not_store_is_refused(string receiptUrl)
    {
        var response = await _client.PostAsJsonAsync("/api/me/wallet/topup/manual", Body(receiptUrl));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // And nothing was filed: a refused request must not leave a pending row
        // for an operator to find.
        await _factory.WithDbAsync(async db =>
            Assert.False(await db.WalletTopUps.AnyAsync(t => t.CustomerId == _customerId)));
    }

    [Fact]
    public async Task A_receipt_from_the_receipts_folder_is_accepted()
    {
        var receiptUrl = OwnReceiptUrl();

        var response = await _client.PostAsJsonAsync("/api/me/wallet/topup/manual", Body(receiptUrl));

        Assert.True(
            response.IsSuccessStatusCode,
            $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.WalletTopUps.AsNoTracking().FirstAsync(t => t.CustomerId == _customerId);

            Assert.Equal(receiptUrl, stored.ReceiptUrl);
            // Filed, never credited: the balance moves only when an operator
            // approves it against the bank statement.
            Assert.Equal(WalletTopUpStatus.Pending, stored.Status);
        });

        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, (await db.Customers.AsNoTracking().FirstAsync(c => c.Id == _customerId))
                .WalletBalance.Amount));
    }

    [Fact]
    public async Task An_unbounded_tracking_number_is_a_field_error_not_a_server_fault()
    {
        var response = await _client.PostAsJsonAsync("/api/me/wallet/topup/manual", new
        {
            amount = 250_000,
            trackingNumber = new string('9', 500),
            paidOn = DateTime.UtcNow.ToString("yyyy-MM-dd"),
            receiptUrl = OwnReceiptUrl(),
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The upload folder the receipt has to come from has to exist, or the
    /// check above turns a required field into an impossible one.
    /// </summary>
    [Fact]
    public async Task Customers_may_upload_into_the_receipts_folder()
    {
        using var content = new MultipartFormDataContent();

        // A one-pixel PNG: the storage adapter sniffs magic bytes, so the
        // declared type alone would not get past it.
        var png = Convert.FromBase64String(
            "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==");

        var file = new ByteArrayContent(png);
        file.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("image/png");
        content.Add(file, "file", "receipt.png");

        var response = await _client.PostAsync("/api/uploads/receipts", content);

        response.EnsureSuccessStatusCode();
    }
}
