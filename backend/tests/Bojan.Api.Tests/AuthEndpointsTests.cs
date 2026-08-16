using Bojan.Domain.Customers;
﻿using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Application.Auth;
using Bojan.Domain.Admin;
using Bojan.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// Exercises <c>/api/auth/otp/request</c>, <c>/api/auth/otp/verify</c> and
/// <c>/api/admin/auth/login</c> over real HTTP against the wired-up application — the
/// same shapes <c>BACKEND.md</c> documents from the frontend's own call
/// sites, verified from the other end.
/// </summary>
/// <remarks>
/// A fresh <see cref="BojanApiFactory"/> per test, not a shared
/// <c>IClassFixture</c> — the rate limiters in <see cref="Endpoints.RateLimitPolicies"/>
/// partition by client address, and every request from an in-process
/// <c>TestServer</c> carries the same synthetic one. A shared host would let
/// an early test's otp requests eat into a later test's limit and fail it
/// with an unrelated 429; a dedicated policy for that behaviour lives in
/// <see cref="RateLimitingTests"/>, deliberately, where the shared bucket is
/// the point.
/// </remarks>
public sealed class AuthEndpointsTests : IDisposable
{
    private readonly BojanApiFactory _factory;
    private readonly HttpClient _client;

    public AuthEndpointsTests()
    {
        _factory = new BojanApiFactory();
        _factory.EnsureDatabaseCreated();
        _client = _factory.CreateClient();
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task Health_endpoint_reports_healthy_against_the_in_memory_database()
    {
        var response = await _client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Otp_request_rejects_a_malformed_phone_number()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone = "12345" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Otp_request_accepts_a_valid_phone_number()
    {
        var response = await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone = "09121110001" });

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    [Fact]
    public async Task Otp_verify_rejects_a_code_with_no_matching_request()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/auth/otp/verify",
            new { phone = "09121110099", code = "11111" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Full_otp_flow_creates_a_customer_and_returns_a_token()
    {
        const string phone = "09121110002";

        var requested = await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        requested.EnsureSuccessStatusCode();
        var code = _factory.Sms.LastCodeFor(phone);

        var verify = await _client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);

        var body = await verify.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.GetProperty("isNewUser").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("token").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(body.GetProperty("userId").GetString()));
    }

    [Fact]
    public async Task Signing_in_twice_recognises_the_returning_customer()
    {
        const string phone = "09121110005";

        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        var first = await _client.PostAsJsonAsync(
            "/api/auth/otp/verify",
            new { phone, code = _factory.Sms.LastCodeFor(phone) });
        var firstBody = await first.Content.ReadFromJsonAsync<JsonElement>();
        var firstUserId = firstBody.GetProperty("userId").GetString();

        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        var second = await _client.PostAsJsonAsync(
            "/api/auth/otp/verify",
            new { phone, code = _factory.Sms.LastCodeFor(phone) });
        var secondBody = await second.Content.ReadFromJsonAsync<JsonElement>();

        Assert.False(secondBody.GetProperty("isNewUser").GetBoolean());
        Assert.Equal(firstUserId, secondBody.GetProperty("userId").GetString());
    }

    [Fact]
    public async Task Verifying_the_same_code_twice_fails_the_second_time()
    {
        const string phone = "09121110003";
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        var code = _factory.Sms.LastCodeFor(phone);

        var first = await _client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        var second = await _client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code });
        Assert.Equal(HttpStatusCode.BadRequest, second.StatusCode);
    }

    [Fact]
    public async Task A_wrong_code_five_times_burns_the_challenge_even_for_the_right_code()
    {
        const string phone = "09121110004";
        await _client.PostAsJsonAsync("/api/auth/otp/request", new { phone });
        var correctCode = _factory.Sms.LastCodeFor(phone);
        var wrongCode = correctCode == "00000" ? "11111" : "00000";

        HttpResponseMessage? last = null;
        for (var attempt = 0; attempt < 5; attempt++)
        {
            last = await _client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = wrongCode });
        }

        // The fifth wrong guess burns the challenge, and a burnt challenge is
        // 429 rather than 400 — the same distinction the frontend's own mock
        // path draws between "wrong code" and "too many attempts".
        Assert.Equal(HttpStatusCode.TooManyRequests, last!.StatusCode);

        var tooLate = await _client.PostAsJsonAsync("/api/auth/otp/verify", new { phone, code = correctCode });
        Assert.Equal(HttpStatusCode.TooManyRequests, tooLate.StatusCode);
    }

    [Fact]
    public async Task Admin_login_rejects_an_unknown_account()
    {
        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { identity = "nobody@bojan.example", password = "irrelevant123" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Admin_login_succeeds_with_the_right_password_and_returns_a_lowercase_role()
    {
        const string password = "correct-horse-battery-staple";
        await SeedAdminAsync("owner@bojan.example", password, AdminRole.Owner);

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { identity = "owner@bojan.example", password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("owner", body.GetProperty("role").GetString());
    }

    [Fact]
    public async Task Admin_login_rejects_the_wrong_password_for_a_real_account()
    {
        await SeedAdminAsync("support@bojan.example", "the-real-password-123", AdminRole.Support);

        var response = await _client.PostAsJsonAsync(
            "/api/admin/auth/login",
            new { identity = "support@bojan.example", password = "not-the-real-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private async Task SeedAdminAsync(string email, string password, AdminRole role)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<BojanDbContext>();
        var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher>();

        var account = new Customer
        {
            Phone = TestData.PhoneFor(email),
            Email = email,
            FirstName = "اپراتور",
            LastName = "آزمون",
            PasswordHash = hasher.Hash(password),
        };
        db.Customers.Add(account);
        await db.SaveChangesAsync();

        db.AdminUsers.Add(new AdminUser
        {
            CustomerId = account.Id,
            Name = "Test Admin",
            Email = email,
            Phone = account.Phone,
            Role = role,
        });

        await db.SaveChangesAsync();
    }
}
