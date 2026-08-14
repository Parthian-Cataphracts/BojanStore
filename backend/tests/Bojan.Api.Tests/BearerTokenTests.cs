using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// The bearer token, used as a bearer token.
/// </summary>
/// <remarks>
/// <para>
/// Every other admin test authenticates through the trusted-proxy headers,
/// because that is how the panel's own write route calls the API. The result
/// was that the JWT half of the same policies — the half that decides what a
/// caller who skipped the panel may do — was minted on every sign-in and never
/// once presented.
/// </para>
/// <para>
/// That gap had teeth. The claims in a token are written by whichever handler
/// mints it, and the old one silently rewrote <c>ClaimTypes.Role</c> to
/// <c>role</c> through an outbound map that the newer handler does not have. A
/// token whose role claim is named anything else still authenticates, still
/// carries the right scope, and authorises nothing — so the failure would have
/// been every operator locked out of every screen, with a build that passed.
/// </para>
/// </remarks>
public sealed class BearerTokenTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _client = null!;

    private const string Password = "correct horse battery 9";

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

    [Fact]
    public async Task An_owner_token_opens_an_owner_only_route()
    {
        var token = await SignInAsync("owner@bojan.example", AdminRole.Owner);

        var response = await GetAsync("/api/admin/settings/audit", token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_support_token_opens_the_support_routes_and_not_the_owner_ones()
    {
        var token = await SignInAsync("support@bojan.example", AdminRole.Support);

        // Both halves matter. The first says the role claim is being read at
        // all — a token with no readable role fails this exactly as a customer
        // token would. The second says it is being read as *support*, which a
        // token that somehow claimed every role would not.
        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/api/admin/support/threads", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/api/admin/settings/audit", token)).StatusCode);
    }

    [Fact]
    public async Task An_admin_token_is_refused_by_the_customer_routes()
    {
        var token = await SignInAsync("crossover@bojan.example", AdminRole.Owner);

        // The scope claim, not the role: an operator credential is not a
        // shopper credential however senior the operator is.
        var response = await GetAsync("/api/me", token);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_customer_token_opens_the_customer_routes()
    {
        var registration = await _client.PostAsJsonAsync(
            "/api/auth/register",
            new { phone = "09121110099", email = "bearer@example.com", password = Password });
        registration.EnsureSuccessStatusCode();

        var token = (await registration.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("token").GetString();

        Assert.Equal(HttpStatusCode.OK, (await GetAsync("/api/me", token)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await GetAsync("/api/admin/settings/audit", token)).StatusCode);
    }

    [Fact]
    public async Task A_token_signed_by_something_else_is_refused()
    {
        var token = await SignInAsync("real@bojan.example", AdminRole.Owner);

        // The last segment is the signature; flipping a character in it leaves
        // a structurally perfect token that verifies against nothing.
        var forged = token![..^2] + (token[^2] == 'a' ? "b" : "a") + token[^1];

        Assert.Equal(HttpStatusCode.Unauthorized, (await GetAsync("/api/admin/settings/audit", forged)).StatusCode);
    }

    private Task<HttpResponseMessage> GetAsync(string path, string? token)
    {
        // No X-Api-Key: the point of these is the token standing on its own,
        // without the trusted-proxy scheme underneath it.
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return _client.SendAsync(request);
    }

    private async Task<string?> SignInAsync(string email, AdminRole role)
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
            var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

            db.AdminUsers.Add(new AdminUser
            {
                Name = "Bearer Test",
                Email = email,
                PasswordHash = hasher.Hash(Password),
                Role = role,
            });

            await db.SaveChangesAsync();
        }

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { identity = email, password = Password });

        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("token").GetString();
    }
}
