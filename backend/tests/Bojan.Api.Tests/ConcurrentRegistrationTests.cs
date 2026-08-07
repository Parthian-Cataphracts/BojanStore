using Bojan.Application.Auth;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// Two sign-ins racing for the same new phone number end up in one account.
/// </summary>
/// <remarks>
/// Verifying a code used to read the customer, see nothing, and insert — two
/// separate steps with room between them. Two verifications arriving together
/// both saw nothing and both inserted, and the unique index on Phone refused
/// the second. The caller was told their sign-in had failed, for an account
/// that by then existed and was theirs.
/// </remarks>
public sealed class ConcurrentRegistrationTests : IAsyncLifetime, IDisposable
{
    private const string Phone = "09121110033";

    private readonly BojanApiFactory _factory = new();

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>
    /// Driven at the repository, not through the endpoint: the race is between
    /// two writes, and an OTP challenge is single-use so the endpoint cannot
    /// legitimately be entered twice for one code.
    /// </summary>
    private async Task<(Guid Id, bool Created)> SignInAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var customers = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();

        var (customer, created) = await customers.GetOrCreateByPhoneAsync(Phone, CancellationToken.None);
        return (customer.Id, created);
    }

    [Fact]
    public async Task The_first_sign_in_creates_the_account()
    {
        var (id, created) = await SignInAsync();

        Assert.True(created);
        Assert.NotEqual(Guid.Empty, id);
    }

    [Fact]
    public async Task A_second_sign_in_finds_the_account_rather_than_creating_another()
    {
        var first = await SignInAsync();
        var second = await SignInAsync();

        Assert.True(first.Created);
        Assert.False(second.Created);
        Assert.Equal(first.Id, second.Id);
    }

    /// <summary>
    /// Both at once, each with its own scope and its own change tracker — the
    /// arrangement that used to produce the failure.
    /// </summary>
    [Fact]
    public async Task Two_at_once_agree_on_one_account()
    {
        var results = await Task.WhenAll(SignInAsync(), SignInAsync());

        Assert.Equal(results[0].Id, results[1].Id);

        // Exactly one of them created it. Which one is a race and does not
        // matter; that both claimed to would.
        Assert.Single(results, result => result.Created);

        await _factory.WithDbAsync(async db =>
            Assert.Equal(1, await db.Customers.CountAsync(c => c.Phone == Phone)));
    }
}
