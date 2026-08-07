using System.Net;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// Who may read the customer base.
/// </summary>
/// <remarks>
/// This was the largest pile of personal data in the panel — every name, phone
/// number, email address and what each person has spent — and it sat behind
/// nothing but "is an operator". The catalogue role, whose whole job is
/// products, could page through all of it. The permission grid narrows this
/// further once an owner opens screen 146, but an installation that never has
/// needs the role gate to be the thing holding.
/// </remarks>
public sealed class CustomerDataAccessTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, "09121110040");
            _customerId = customer.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    private async Task<HttpClient> ClientForAsync(AdminRole role)
    {
        Guid adminId = default;
        await _factory.WithDbAsync(async db =>
            adminId = (await TestData.AddAdminAsync(db, role, $"{role}@bojan.test")).Id);

        return _factory.CreateAdminClient(adminId);
    }

    [Theory]
    [InlineData(AdminRole.Owner)]
    [InlineData(AdminRole.Sales)]
    [InlineData(AdminRole.Support)]
    public async Task The_roles_that_deal_with_people_can_read_the_list(AdminRole role)
    {
        using var client = await ClientForAsync(role);

        (await client.GetAsync("/api/admin/customers")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_catalogue_role_cannot_read_the_list()
    {
        using var client = await ClientForAsync(AdminRole.Product);

        var response = await client.GetAsync("/api/admin/customers");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    /// <summary>One record is the same data as the list, one row at a time.</summary>
    [Fact]
    public async Task The_catalogue_role_cannot_read_one_record_either()
    {
        using var client = await ClientForAsync(AdminRole.Product);

        var response = await client.GetAsync($"/api/admin/customers/{_customerId}");
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
