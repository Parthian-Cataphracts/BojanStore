using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Email link verification, phone code verification, and changing the account
/// phone through it — <c>/api/account/email/...</c> and
/// <c>/api/account/phone/...</c>.
/// </summary>
public sealed class AccountVerificationEndpointsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _customerId;
    private const string Phone = "09121110090";
    private const string Email = "verify@bojan.test";

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            var customer = await Bojan.Api.Tests.TestData.AddCustomerAsync(db, Phone);
            customer.Email = Email;
            await db.SaveChangesAsync();
            _customerId = customer.Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    // --- email verification --------------------------------------------------

    [Fact]
    public async Task Requesting_email_verification_sends_a_link_and_confirming_it_marks_the_email_verified()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        var request = await client.PostAsync("/api/account/email/verify/request", null);
        Assert.Equal(HttpStatusCode.NoContent, request.StatusCode);

        var token = _factory.Email.ResetTokenFor(Email);
        Assert.NotNull(token);

        using var anonymous = _factory.CreateClient();
        var confirm = await anonymous.GetAsync($"/api/account/email/verify/confirm?token={token}");
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.True((await db.Customers.SingleAsync(c => c.Id == _customerId)).IsEmailVerified));
    }

    [Fact]
    public async Task A_second_email_verification_request_is_refused_while_the_first_link_is_still_live()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsync("/api/account/email/verify/request", null)).EnsureSuccessStatusCode();

        var second = await client.PostAsync("/api/account/email/verify/request", null);

        Assert.Equal(HttpStatusCode.TooManyRequests, second.StatusCode);
    }

    [Fact]
    public async Task Confirming_an_unknown_email_token_fails()
    {
        using var anonymous = _factory.CreateClient();

        var response = await anonymous.GetAsync("/api/account/email/verify/confirm?token=not-a-real-token");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Requesting_email_verification_with_no_email_on_file_is_refused()
    {
        Guid noEmailCustomerId = default;
        await _factory.WithDbAsync(async db =>
        {
            var customer = await Bojan.Api.Tests.TestData.AddCustomerAsync(db, "09121110091");
            noEmailCustomerId = customer.Id;
        });

        using var client = _factory.CreateCustomerClient(noEmailCustomerId);

        var response = await client.PostAsync("/api/account/email/verify/request", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>Changing the email resets the verified flag — a stamp on an address nobody confirmed is worse than none.</summary>
    [Fact]
    public async Task Changing_the_email_resets_the_verified_flag()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsync("/api/account/email/verify/request", null)).EnsureSuccessStatusCode();
        var token = _factory.Email.ResetTokenFor(Email)!;
        (await client.GetAsync($"/api/account/email/verify/confirm?token={token}")).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.True((await db.Customers.SingleAsync(c => c.Id == _customerId)).IsEmailVerified));

        var update = await client.PutAsJsonAsync("/api/me", new { email = "new-address@bojan.test" });
        update.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.False((await db.Customers.SingleAsync(c => c.Id == _customerId)).IsEmailVerified));
    }

    // --- phone verification ---------------------------------------------------

    [Fact]
    public async Task Requesting_verification_of_the_current_phone_sends_a_code_and_confirming_marks_it_verified()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsync("/api/account/phone/verify/request", null)).EnsureSuccessStatusCode();

        var code = _factory.Sms.LastCodeFor(Phone);

        var confirm = await client.PostAsJsonAsync("/api/account/phone/verify/confirm", new { code });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            Assert.True(customer.IsPhoneVerified);
            Assert.Equal(Phone, customer.Phone);
        });
    }

    [Fact]
    public async Task Confirming_a_wrong_phone_code_does_not_verify_the_phone()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsync("/api/account/phone/verify/request", null)).EnsureSuccessStatusCode();

        var response = await client.PostAsJsonAsync("/api/account/phone/verify/confirm", new { code = "00000" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db =>
            Assert.False((await db.Customers.SingleAsync(c => c.Id == _customerId)).IsPhoneVerified));
    }

    // --- phone change -----------------------------------------------------------

    [Fact]
    public async Task Requesting_a_phone_change_and_confirming_it_applies_the_new_number()
    {
        const string newPhone = "09121110092";
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsJsonAsync("/api/account/phone/change/request", new { newPhone }))
            .EnsureSuccessStatusCode();

        var code = _factory.Sms.LastCodeFor(newPhone);

        var confirm = await client.PostAsJsonAsync("/api/account/phone/verify/confirm", new { code });
        Assert.Equal(HttpStatusCode.NoContent, confirm.StatusCode);

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Id == _customerId);
            Assert.Equal(newPhone, customer.Phone);
            Assert.True(customer.IsPhoneVerified);
        });
    }

    /// <summary>The old number must not still work — the account really moved.</summary>
    [Fact]
    public async Task After_a_confirmed_phone_change_the_old_number_no_longer_belongs_to_the_account()
    {
        const string newPhone = "09121110093";
        using var client = _factory.CreateCustomerClient(_customerId);

        (await client.PostAsJsonAsync("/api/account/phone/change/request", new { newPhone }))
            .EnsureSuccessStatusCode();
        var code = _factory.Sms.LastCodeFor(newPhone);
        (await client.PostAsJsonAsync("/api/account/phone/verify/confirm", new { code }))
            .EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
            Assert.False(await db.Customers.AnyAsync(c => c.Phone == Phone)));
    }

    [Fact]
    public async Task Requesting_a_phone_change_to_a_number_already_taken_is_refused()
    {
        const string takenPhone = "09121110094";
        await _factory.WithDbAsync(db => Bojan.Api.Tests.TestData.AddCustomerAsync(db, takenPhone));

        using var client = _factory.CreateCustomerClient(_customerId);

        var response = await client.PostAsJsonAsync("/api/account/phone/change/request", new { newPhone = takenPhone });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_phone_number_is_rejected_by_validation()
    {
        using var client = _factory.CreateCustomerClient(_customerId);

        var response = await client.PostAsJsonAsync("/api/account/phone/change/request", new { newPhone = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
