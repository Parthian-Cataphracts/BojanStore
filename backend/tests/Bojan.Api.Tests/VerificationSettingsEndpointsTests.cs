using System.Net;
using System.Net.Http.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// The panel's verification settings screen — <c>/api/admin/settings/verification</c>.
/// Owner only, both toggles off by default.
/// </summary>
public sealed class VerificationSettingsEndpointsTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private Guid _ownerId;
    private Guid _supportId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        await _factory.WithDbAsync(async db =>
        {
            _ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner-verif@bojan.test")).Id;
            _supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "support-verif@bojan.test")).Id;
        });
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    [Fact]
    public async Task Both_toggles_default_to_off()
    {
        using var client = _factory.CreateAdminClient(_ownerId);

        var response = await client.GetAsync("/api/admin/settings/verification");
        response.EnsureSuccessStatusCode();

        var body = await response.Content.ReadFromJsonAsync<VerificationSettingsResponse>();
        Assert.NotNull(body);
        Assert.False(body!.RequireEmailVerification);
        Assert.False(body.RequirePhoneVerification);
    }

    [Fact]
    public async Task An_owner_can_turn_the_toggles_on_and_read_them_back()
    {
        using var client = _factory.CreateAdminClient(_ownerId);

        var save = await client.PutAsJsonAsync(
            "/api/admin/settings/verification",
            new { requireEmailVerification = true, requirePhoneVerification = true });
        Assert.Equal(HttpStatusCode.NoContent, save.StatusCode);

        var response = await client.GetAsync("/api/admin/settings/verification");
        var body = await response.Content.ReadFromJsonAsync<VerificationSettingsResponse>();

        Assert.True(body!.RequireEmailVerification);
        Assert.True(body.RequirePhoneVerification);
    }

    [Fact]
    public async Task Turning_one_toggle_on_does_not_affect_the_other()
    {
        using var client = _factory.CreateAdminClient(_ownerId);

        (await client.PutAsJsonAsync(
                "/api/admin/settings/verification",
                new { requireEmailVerification = true, requirePhoneVerification = false }))
            .EnsureSuccessStatusCode();

        var response = await client.GetAsync("/api/admin/settings/verification");
        var body = await response.Content.ReadFromJsonAsync<VerificationSettingsResponse>();

        Assert.True(body!.RequireEmailVerification);
        Assert.False(body.RequirePhoneVerification);
    }

    [Fact]
    public async Task A_non_owner_cannot_read_the_settings()
    {
        using var client = _factory.CreateAdminClient(_supportId);

        var response = await client.GetAsync("/api/admin/settings/verification");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task A_non_owner_cannot_save_the_settings()
    {
        using var client = _factory.CreateAdminClient(_supportId);

        var response = await client.PutAsJsonAsync(
            "/api/admin/settings/verification",
            new { requireEmailVerification = true, requirePhoneVerification = true });

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    private sealed record VerificationSettingsResponse(bool RequireEmailVerification, bool RequirePhoneVerification);
}
