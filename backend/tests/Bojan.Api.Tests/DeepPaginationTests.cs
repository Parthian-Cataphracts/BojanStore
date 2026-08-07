using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// A page number past the end of a list is an empty page, not a fault.
/// </summary>
/// <remarks>
/// Only the floor was clamped. <c>?page=20000000&amp;pageSize=200</c> multiplied
/// out past <see cref="int.MaxValue"/>, wrapped negative, and reached the
/// database as a negative <c>OFFSET</c> — so a crawler following a stale link,
/// or anyone editing the address bar, got a 500 out of every paged list in the
/// shop.
/// </remarks>
public sealed class DeepPaginationTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            var (brandId, categoryId) = await TestData.AddCatalogueAsync(db);
            await TestData.AddProductAsync(db, brandId, categoryId, "p-01", 100_000, stock: 5);
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        _admin = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _admin?.Dispose();
        _factory.Dispose();
    }

    [Theory]
    [InlineData("/api/products?page=20000000&pageSize=100")]
    [InlineData("/api/products?page=2147483647&pageSize=100")]
    public async Task The_public_catalogue_answers_an_absurd_page_with_an_empty_one(string path)
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData("/api/admin/orders?page=20000000&pageSize=200")]
    [InlineData("/api/admin/products?page=2147483647&pageSize=200")]
    [InlineData("/api/admin/customers?page=999999999&pageSize=200")]
    public async Task A_panel_list_answers_an_absurd_page_with_an_empty_one(string path)
    {
        var response = await _admin.GetAsync(path);
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Empty(body.GetProperty("items").EnumerateArray());
    }

    /// <summary>The first page still works — the clamp must not have moved the floor.</summary>
    [Fact]
    public async Task The_first_page_still_returns_rows()
    {
        using var anonymous = _factory.CreateClient();

        var body = await (await anonymous.GetAsync("/api/products?page=1"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.NotEmpty(body.GetProperty("items").EnumerateArray());
    }
}
