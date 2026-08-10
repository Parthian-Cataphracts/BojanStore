using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// The maintenance switch, from the screen that flips it to the endpoint the
/// storefront's middleware asks.
/// </summary>
/// <remarks>
/// Both halves worked and were wired to different rows: the panel saved
/// <c>store/maintenance</c> and the read looked for <c>general/maintenance</c>,
/// a section nothing in the product has ever written. So the switch reported
/// itself on and the shop stayed open. Testing either half alone would have
/// passed, which is why this test spans both.
/// </remarks>
public sealed class MaintenanceModeTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _visitor = null!;
    private HttpClient _operator = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
        });

        _visitor = _factory.CreateClient();
        _operator = _factory.CreateAdminClient(ownerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _visitor?.Dispose();
        _operator?.Dispose();
        _factory.Dispose();
    }

    /// <summary>Exactly what `SettingsForm` posts when the switch is flipped.</summary>
    private Task<HttpResponseMessage> SetSwitchAsync(string value) =>
        _operator.PostAsJsonAsync("/api/admin/settings", new
        {
            section = "store",
            values = new Dictionary<string, string> { ["maintenance"] = value },
        });

    private async Task<bool> StorefrontSeesMaintenanceAsync()
    {
        var response = await _visitor.GetAsync("/api/store/status");
        response.EnsureSuccessStatusCode();
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("maintenanceMode").GetBoolean();
    }

    [Fact]
    public async Task The_shop_is_open_until_the_switch_is_turned_on()
    {
        Assert.False(await StorefrontSeesMaintenanceAsync());
    }

    [Fact]
    public async Task Turning_the_switch_on_closes_the_shop_and_turning_it_off_reopens_it()
    {
        (await SetSwitchAsync("true")).EnsureSuccessStatusCode();
        Assert.True(await StorefrontSeesMaintenanceAsync());

        (await SetSwitchAsync("false")).EnsureSuccessStatusCode();
        Assert.False(await StorefrontSeesMaintenanceAsync());
    }

    /// <summary>
    /// The panel reads the same row back to draw the switch — so an operator
    /// who turns it on and reloads sees it on, rather than a control that
    /// forgets what they just told it.
    /// </summary>
    [Fact]
    public async Task The_panel_reads_the_switch_back_as_it_was_saved()
    {
        (await SetSwitchAsync("true")).EnsureSuccessStatusCode();

        var section = await (await _operator.GetAsync("/api/admin/settings/store"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("true", section.GetProperty("maintenance").GetString());
    }
}
