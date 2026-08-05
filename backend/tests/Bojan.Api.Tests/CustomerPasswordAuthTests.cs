using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// The password door, beside the one-time code.
/// </summary>
/// <remarks>
/// It exists because SMS delivery is the weak link: a shop whose only way in is
/// a text message loses the customers whose message never arrives. These cover
/// the rules that make a second door safe rather than a second weakness.
/// </remarks>
public sealed class CustomerPasswordAuthTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    private const string Phone = "09121110020";
    private const string Email = "shopper@example.com";
    private const string Password = "correct horse 7";

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _client?.Dispose();
        _factory.Dispose();
    }

    private Task<HttpResponseMessage> Register(string phone = Phone, string email = Email, string password = Password) =>
        _client.PostAsJsonAsync("/api/auth/register", new { phone, email, password });

    private Task<HttpResponseMessage> Login(string identity, string password) =>
        _client.PostAsJsonAsync("/api/auth/login", new { identity, password });

    // --- registering ---------------------------------------------------------

    [Fact]
    public async Task Registering_creates_the_account_and_signs_it_in()
    {
        var response = await Register();
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isNewUser").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));

        await _factory.WithDbAsync(async db =>
        {
            var customer = await db.Customers.SingleAsync(c => c.Phone == Phone);
            Assert.Equal(Email, customer.Email);
            // Stored hashed, never as typed.
            Assert.NotNull(customer.PasswordHash);
            Assert.DoesNotContain(Password, customer.PasswordHash);
        });
    }

    [Fact]
    public async Task A_phone_that_already_has_an_account_cannot_register_again()
    {
        (await Register()).EnsureSuccessStatusCode();

        var again = await Register(email: "someone.else@example.com");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    /// <summary>An email has to identify one account, or a reset is ambiguous about whose password it changes.</summary>
    [Fact]
    public async Task An_email_already_in_use_cannot_register_again()
    {
        (await Register()).EnsureSuccessStatusCode();

        var again = await Register(phone: "09121110021");
        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
    }

    [Theory]
    [InlineData("short1")]          // under the minimum
    [InlineData("allletters")]      // no digit
    [InlineData("1234567890")]      // no letter
    public async Task A_password_the_policy_refuses_does_not_create_an_account(string password)
    {
        var response = await Register(password: password);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        await _factory.WithDbAsync(async db => Assert.False(await db.Customers.AnyAsync()));
    }

    // --- signing in ----------------------------------------------------------

    [Fact]
    public async Task The_password_signs_in_by_phone_or_by_email()
    {
        (await Register()).EnsureSuccessStatusCode();

        (await Login(Phone, Password)).EnsureSuccessStatusCode();
        (await Login(Email, Password)).EnsureSuccessStatusCode();
        // However it was typed — one address is one account.
        (await Login("SHOPPER@Example.COM", Password)).EnsureSuccessStatusCode();
    }

    /// <summary>
    /// Every failure is the same 401.
    /// </summary>
    /// <remarks>
    /// A different answer for "no such account" than for "wrong password" is a
    /// way to ask the shop which phone numbers and addresses it knows.
    /// </remarks>
    [Fact]
    public async Task An_unknown_identity_and_a_wrong_password_fail_identically()
    {
        (await Register()).EnsureSuccessStatusCode();

        var wrongPassword = await Login(Phone, "wrong password 1");
        var unknownPhone = await Login("09129990000", Password);
        var unknownEmail = await Login("nobody@example.com", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownPhone.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
    }

    /// <summary>
    /// An account that only ever used a code has no password to match.
    /// </summary>
    /// <remarks>
    /// The nullable hash is the point: those accounts keep working through the
    /// code path, and the password path must refuse them rather than treat a
    /// null hash as "anything matches".
    /// </remarks>
    [Fact]
    public async Task An_account_with_no_password_cannot_be_signed_into_with_one()
    {
        await _factory.WithDbAsync(async db =>
        {
            await TestData.AddCustomerAsync(db, "09121110030");
        });

        var response = await Login("09121110030", Password);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    // --- forgetting and resetting -------------------------------------------

    [Fact]
    public async Task A_reset_link_sets_a_new_password_and_cannot_be_used_twice()
    {
        (await Register()).EnsureSuccessStatusCode();

        var asked = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
        Assert.Equal(HttpStatusCode.NoContent, asked.StatusCode);

        // Read out of the link in the message, because that is the only copy
        // the customer has — and the link is what makes it usable to them.
        var token = _factory.Email.ResetTokenFor(Email);
        Assert.False(string.IsNullOrWhiteSpace(token));

        var reset = await _client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, password = "brand new pass 9" });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        (await Login(Phone, "brand new pass 9")).EnsureSuccessStatusCode();
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(Phone, Password)).StatusCode);

        // Spent. A link that stayed live would undo the password it just set.
        var replay = await _client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token, password = "third password 3" });
        Assert.Equal(HttpStatusCode.BadRequest, replay.StatusCode);
    }

    /// <summary>
    /// The token is stored hashed, so the table is worth nothing if it leaks.
    /// </summary>
    [Fact]
    public async Task The_emailed_token_is_not_what_is_stored()
    {
        (await Register()).EnsureSuccessStatusCode();
        await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });

        var token = _factory.Email.ResetTokenFor(Email)!;

        await _factory.WithDbAsync(async db =>
        {
            var stored = await db.PasswordResetTokens.SingleAsync();
            Assert.NotEqual(token, stored.TokenHash);
        });
    }

    /// <summary>
    /// An address the shop does not know gets the same answer as one it does.
    /// </summary>
    /// <remarks>
    /// Otherwise this endpoint answers "does this person shop here?" for any
    /// address someone cares to try.
    /// </remarks>
    [Fact]
    public async Task Asking_to_reset_an_unknown_address_is_indistinguishable_from_a_known_one()
    {
        (await Register()).EnsureSuccessStatusCode();

        var known = await _client.PostAsJsonAsync("/api/auth/forgot-password", new { email = Email });
        var unknown = await _client.PostAsJsonAsync(
            "/api/auth/forgot-password", new { email = "stranger@example.com" });

        Assert.Equal(known.StatusCode, unknown.StatusCode);
        Assert.Equal(HttpStatusCode.NoContent, unknown.StatusCode);

        // And nothing was sent to the address that has no account.
        Assert.Null(_factory.Email.LastBodyFor("stranger@example.com"));
    }

    [Fact]
    public async Task An_unknown_reset_token_is_refused()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/reset-password", new { token = new string('a', 64), password = "brand new pass 9" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
