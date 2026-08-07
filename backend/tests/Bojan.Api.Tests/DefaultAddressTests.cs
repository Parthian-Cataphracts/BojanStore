using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// A customer with any address has exactly one default.
/// </summary>
/// <remarks>
/// The invariant the rest of the system assumes — the checkout pre-selects the
/// default and the account screen marks it — but which only creation upheld.
/// Deleting the default left a list with nothing marked and a checkout that
/// pre-selected nothing, permanently; and unticking the box on the form did
/// nothing at all, because the save only ever set the flag and never cleared
/// it.
/// </remarks>
public sealed class DefaultAddressTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _customer = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
            _customerId = (await TestData.AddCustomerAsync(db, "09121110070")).Id);

        _customer = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _customer?.Dispose();
        _factory.Dispose();
    }

    private async Task<Guid> SaveAsync(string title, bool isDefault)
    {
        var response = await _customer.PostAsJsonAsync("/api/me/addresses", new
        {
            title,
            recipient = "گیرنده آزمایشی",
            phone = "09121110070",
            province = "تهران",
            city = "تهران",
            postalCode = "1234567890",
            line = "خیابان آزمایشی، پلاک ۱",
            isDefault,
        });

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return Guid.Parse(body.GetProperty("id").GetString()!);
    }

    private async Task<Guid?> DefaultAsync()
    {
        Guid? id = null;
        await _factory.WithDbAsync(async db =>
            id = (await db.Addresses.SingleOrDefaultAsync(a => a.CustomerId == _customerId && a.IsDefault))?.Id);
        return id;
    }

    [Fact]
    public async Task The_first_address_is_the_default_whether_or_not_the_box_was_ticked()
    {
        var first = await SaveAsync("خانه", isDefault: false);

        Assert.Equal(first, await DefaultAsync());
    }

    [Fact]
    public async Task Deleting_the_default_hands_the_title_to_another_address()
    {
        var first = await SaveAsync("خانه", isDefault: true);
        var second = await SaveAsync("محل کار", isDefault: false);

        (await _customer.PostAsJsonAsync("/api/me/addresses/delete", new { id = first.ToString() }))
            .EnsureSuccessStatusCode();

        Assert.Equal(second, await DefaultAsync());
    }

    [Fact]
    public async Task Deleting_the_last_address_leaves_nothing_to_promote()
    {
        var only = await SaveAsync("خانه", isDefault: true);

        (await _customer.PostAsJsonAsync("/api/me/addresses/delete", new { id = only.ToString() }))
            .EnsureSuccessStatusCode();

        Assert.Null(await DefaultAsync());
        await _factory.WithDbAsync(async db =>
            Assert.Equal(0, await db.Addresses.CountAsync(a => a.CustomerId == _customerId)));
    }

    /// <summary>Unticking the box on the form used to be silently ignored.</summary>
    [Fact]
    public async Task Unticking_the_default_moves_it_rather_than_doing_nothing()
    {
        var first = await SaveAsync("خانه", isDefault: true);
        var second = await SaveAsync("محل کار", isDefault: false);

        var response = await _customer.PostAsJsonAsync("/api/me/addresses", new
        {
            id = first.ToString(),
            title = "خانه",
            recipient = "گیرنده آزمایشی",
            phone = "09121110070",
            province = "تهران",
            city = "تهران",
            postalCode = "1234567890",
            line = "خیابان آزمایشی، پلاک ۱",
            isDefault = false,
        });

        response.EnsureSuccessStatusCode();

        Assert.Equal(second, await DefaultAsync());
    }

    /// <summary>
    /// The only address keeps the flag whatever the form says: a customer with
    /// an address and no default is the state the checkout cannot work with.
    /// </summary>
    [Fact]
    public async Task Unticking_the_only_address_leaves_it_default()
    {
        var only = await SaveAsync("خانه", isDefault: true);

        var response = await _customer.PostAsJsonAsync("/api/me/addresses", new
        {
            id = only.ToString(),
            title = "خانه",
            recipient = "گیرنده آزمایشی",
            phone = "09121110070",
            province = "تهران",
            city = "تهران",
            postalCode = "1234567890",
            line = "خیابان آزمایشی، پلاک ۱",
            isDefault = false,
        });

        response.EnsureSuccessStatusCode();

        Assert.Equal(only, await DefaultAsync());
    }
}
