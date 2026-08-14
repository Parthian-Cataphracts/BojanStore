using System.Net;
using System.Net.Http.Json;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// An operator signing in on the storefront.
/// </summary>
/// <remarks>
/// <para>
/// Their credentials opened the panel and nothing else, so the person who runs
/// the shop needed a second account with a second password to buy from it — and
/// nothing recorded that the two were the same person.
/// </para>
/// <para>
/// One password, in one place: this verifies against the operator record and
/// signs the caller in as their linked shopping account, so there is no second
/// hash to keep in step. The link is made on the first storefront sign-in,
/// because most operators never shop.
/// </para>
/// </remarks>
public sealed class OperatorAsShopperTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    private const string Password = "operatorPassword123";

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task<Guid> AddOperatorAsync(string email, string? phone, bool active = true)
    {
        var id = Guid.Empty;

        await _factory.WithDbAsync(async db =>
        {
            using var scope = _factory.Services.CreateScope();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            var account = new AdminUser
            {
                Name = "نگار مرادی",
                Email = email,
                Phone = phone,
                PasswordHash = hasher.Hash(Password),
                Role = AdminRole.Support,
                IsActive = active,
            };

            db.AdminUsers.Add(account);
            await db.SaveChangesAsync();
            id = account.Id;
        });

        return id;
    }

    private Task<HttpResponseMessage> SignIn(string identity, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { identity, password });

    private sealed record LoginBody(string? Token, string? Phone);

    private sealed record Problem(string? Title, string? Detail);

    [Fact]
    public async Task An_operator_signs_in_on_the_storefront_with_their_panel_password()
    {
        await AddOperatorAsync("shopper-operator@bojan.test", "09121230001");

        var response = await SignIn("shopper-operator@bojan.test", Password);

        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<LoginBody>();
        Assert.False(string.IsNullOrWhiteSpace(body!.Token));
    }

    [Fact]
    public async Task The_first_sign_in_creates_the_shopping_account_and_links_it()
    {
        var operatorId = await AddOperatorAsync("linked@bojan.test", "09121230002");

        (await SignIn("linked@bojan.test", Password)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var account = await db.AdminUsers.AsNoTracking().SingleAsync(a => a.Id == operatorId);
            Assert.NotNull(account.CustomerId);

            var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == account.CustomerId);
            Assert.Equal("09121230002", customer.Phone);
            // The name comes across so the shop is not addressing its own staff
            // as an unnamed account.
            Assert.Equal("نگار", customer.FirstName);
            Assert.Equal("مرادی", customer.LastName);
        });
    }

    /// <summary>
    /// The second sign-in must reach the same shopping account, or every visit
    /// would strand the last one's orders on a row nobody can reach.
    /// </summary>
    [Fact]
    public async Task Signing_in_again_reuses_the_same_shopping_account()
    {
        await AddOperatorAsync("stable@bojan.test", "09121230003");

        var first = await (await SignIn("stable@bojan.test", Password)).Content.ReadFromJsonAsync<LoginBody>();
        var second = await (await SignIn("stable@bojan.test", Password)).Content.ReadFromJsonAsync<LoginBody>();

        Assert.Equal(first!.Phone, second!.Phone);

        var customers = 0;
        await _factory.WithDbAsync(async db =>
            customers = await db.Customers.CountAsync(c => c.Phone == "09121230003"));

        Assert.Equal(1, customers);
    }

    /// <summary>
    /// An operator who already shops here under that number is the same person,
    /// and their orders have to stay theirs.
    /// </summary>
    [Fact]
    public async Task An_existing_customer_on_that_number_is_linked_rather_than_duplicated()
    {
        Guid existingId = default;
        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, "09121230004");
            customer.FirstName = "نام";
            customer.LastName = "قبلی";
            await db.SaveChangesAsync();
            existingId = customer.Id;
        });

        var operatorId = await AddOperatorAsync("already-a-customer@bojan.test", "09121230004");

        (await SignIn("already-a-customer@bojan.test", Password)).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var account = await db.AdminUsers.AsNoTracking().SingleAsync(a => a.Id == operatorId);
            Assert.Equal(existingId, account.CustomerId);

            // Their own name is left alone. Overwriting it because an operator
            // shares the number would be editing somebody else's account.
            var customer = await db.Customers.AsNoTracking().SingleAsync(c => c.Id == existingId);
            Assert.Equal("نام", customer.FirstName);
        });
    }

    [Fact]
    public async Task A_wrong_password_is_refused_exactly_as_a_customer_s_would_be()
    {
        await AddOperatorAsync("wrong-password@bojan.test", "09121230005");

        var response = await SignIn("wrong-password@bojan.test", "notThePassword123");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A suspended operator is suspended everywhere. Letting them in through
    /// the storefront would make the panel's suspension a door rather than a
    /// lock.
    /// </summary>
    [Fact]
    public async Task A_suspended_operator_cannot_sign_in_on_the_storefront_either()
    {
        await AddOperatorAsync("suspended@bojan.test", "09121230006", active: false);

        var response = await SignIn("suspended@bojan.test", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// A customer is a phone number — it is the unique sign-in key and the
    /// column is required — so an operator without one has nothing to build a
    /// shopping account from. The answer names the field rather than pretending
    /// the password was wrong.
    /// </summary>
    [Fact]
    public async Task An_operator_with_no_phone_is_told_which_field_is_missing()
    {
        await AddOperatorAsync("no-phone@bojan.test", phone: null);

        var response = await SignIn("no-phone@bojan.test", Password);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<Problem>();
        Assert.Equal("operator-needs-phone", problem!.Detail);
    }

    /// <summary>
    /// The customer path still owns its own accounts: an ordinary shopper's
    /// password must not be checked against the operator table on the way past.
    /// </summary>
    [Fact]
    public async Task An_ordinary_customer_still_signs_in_the_way_they_did()
    {
        Guid customerId = default;
        await _factory.WithDbAsync(async db =>
            customerId = (await TestData.AddCustomerAsync(db, "09121230007")).Id);

        var owner = Guid.Empty;
        await _factory.WithDbAsync(async db =>
            owner = (await TestData.AddAdminAsync(db, AdminRole.Owner, "shopper-owner@bojan.test")).Id);

        var admin = _factory.CreateAdminClient(owner);
        (await admin.PostAsJsonAsync(
            "/api/admin/customers/password",
            new { customerId = customerId.ToString(), password = "shopperPassword123" }))
            .EnsureSuccessStatusCode();

        var response = await SignIn("09121230007", "shopperPassword123");

        response.EnsureSuccessStatusCode();
    }
}
