using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Microsoft.EntityFrameworkCore;

namespace Bojan.Api.Tests;

/// <summary>
/// Browser notifications end to end: the owner switching the channel on, and a
/// customer registering a browser against it.
/// </summary>
/// <remarks>
/// The wire format is pinned separately against the RFC — see
/// <see cref="WebPushCryptoTests"/>. What these cover is everything around it:
/// that a shop with no key pair cannot claim to have the channel on, that the
/// public key is published and the private one never is, and that a
/// subscription cannot be filed against somebody else's account.
/// </remarks>
public sealed class PushSubscriptionTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private HttpClient _customer = null!;
    private Guid _customerId;

    /// <summary>A real subscription's shape: 65 and 16 bytes, unpadded base64url.</summary>
    private const string BrowserKey = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string BrowserAuth = "BTBZMqHH6r4Tts7J_aSIgg";
    private const string Endpoint = "https://fcm.googleapis.com/fcm/send/first-browser";

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var owner = await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@example.com");
            var customer = await TestData.AddCustomerAsync(db, "09120000001");

            ownerId = owner.Id;
            _customerId = customer.Id;
        });

        _owner = _factory.CreateAdminClient(ownerId);
        _customer = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _owner?.Dispose();
        _customer?.Dispose();
        _factory.Dispose();
    }

    /// <summary>Generates a pair and switches the channel on — the settings screen in two calls.</summary>
    private async Task<string> EnablePushAsync()
    {
        var generated = await _owner.PostAsJsonAsync("/api/admin/push/settings/keys", new { confirm = true });
        generated.EnsureSuccessStatusCode();

        var settings = await generated.Content.ReadFromJsonAsync<JsonElement>();
        var publicKey = settings.GetProperty("publicKey").GetString()!;

        var saved = await _owner.PostAsJsonAsync("/api/admin/push/settings", new
        {
            enabled = true,
            subject = "mailto:shop@bojan.test",
        });

        saved.EnsureSuccessStatusCode();

        return publicKey;
    }

    private Task<HttpResponseMessage> SubscribeAsync(string endpoint = Endpoint) =>
        _customer.PostAsJsonAsync("/api/me/push/subscribe", new
        {
            endpoint,
            p256dh = BrowserKey,
            auth = BrowserAuth,
        });

    /// <summary>
    /// A shop that has generated no keys cannot switch push on. Enabled with no
    /// key pair reports a channel that queues broadcasts nothing can deliver.
    /// </summary>
    [Fact]
    public async Task Push_cannot_be_switched_on_before_keys_exist()
    {
        var response = await _owner.PostAsJsonAsync("/api/admin/push/settings", new
        {
            enabled = true,
            subject = "mailto:shop@bojan.test",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// The public key is published material — a browser cannot subscribe without
    /// it — and the private half never leaves the server in either direction.
    /// </summary>
    [Fact]
    public async Task The_public_key_is_published_and_the_private_one_is_not()
    {
        var publicKey = await EnablePushAsync();

        var settings = await _owner.GetFromJsonAsync<JsonElement>("/api/admin/push/settings");

        Assert.Equal(publicKey, settings.GetProperty("publicKey").GetString());
        Assert.True(settings.GetProperty("hasPrivateKey").GetBoolean());

        // Not "empty" — absent. There is no field a private key could travel in.
        Assert.False(settings.TryGetProperty("privateKey", out _));

        // And anyone at all can read what they need to subscribe.
        using var anonymous = _factory.CreateClient();
        var availability = await anonymous.GetFromJsonAsync<JsonElement>("/api/push/availability");

        Assert.True(availability.GetProperty("enabled").GetBoolean());
        Assert.Equal(publicKey, availability.GetProperty("publicKey").GetString());
    }

    /// <summary>
    /// Before the owner has switched it on, the storefront is told nothing —
    /// including the key, which would otherwise let a browser subscribe to a
    /// shop that cannot sign a message to it.
    /// </summary>
    [Fact]
    public async Task An_unconfigured_shop_hands_out_no_key()
    {
        using var anonymous = _factory.CreateClient();
        var availability = await anonymous.GetFromJsonAsync<JsonElement>("/api/push/availability");

        Assert.False(availability.GetProperty("enabled").GetBoolean());
        Assert.Empty(availability.GetProperty("publicKey").GetString()!);
    }

    [Fact]
    public async Task A_browser_can_be_registered_and_forgotten()
    {
        await EnablePushAsync();

        (await SubscribeAsync()).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var subscription = await db.PushSubscriptions.SingleAsync();

            Assert.Equal(_customerId, subscription.CustomerId);
            Assert.Equal(Endpoint, subscription.Endpoint);
        });

        var forgotten = await _customer.PostAsJsonAsync(
            "/api/me/push/unsubscribe", new { endpoint = Endpoint });

        forgotten.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db => Assert.False(await db.PushSubscriptions.AnyAsync()));
    }

    /// <summary>
    /// The same browser subscribing twice is one row. Its keys rotate when it
    /// renews, so the second call updates rather than duplicating — and the
    /// unique index on the endpoint would refuse a duplicate anyway.
    /// </summary>
    [Fact]
    public async Task Re_subscribing_the_same_browser_updates_one_row()
    {
        await EnablePushAsync();

        (await SubscribeAsync()).EnsureSuccessStatusCode();
        (await SubscribeAsync()).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db => Assert.Equal(1, await db.PushSubscriptions.CountAsync()));
    }

    /// <summary>
    /// One person on a phone and a laptop is two browsers, and both should hear
    /// about their order.
    /// </summary>
    [Fact]
    public async Task Two_browsers_for_one_customer_are_two_rows()
    {
        await EnablePushAsync();

        (await SubscribeAsync()).EnsureSuccessStatusCode();
        (await SubscribeAsync("https://updates.push.services.mozilla.com/wpush/v2/second")).EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db => Assert.Equal(2, await db.PushSubscriptions.CountAsync()));
    }

    /// <summary>
    /// An endpoint has to be an HTTPS URL at a push service. Anything else is a
    /// broken client, or an attempt to make the shop's server issue signed
    /// requests at a host of somebody else's choosing.
    /// </summary>
    [Theory]
    [InlineData("http://fcm.googleapis.com/fcm/send/x")]
    [InlineData("/fcm/send/x")]
    [InlineData("file:///etc/passwd")]
    [InlineData("")]
    public async Task An_endpoint_that_is_not_an_https_url_is_refused(string endpoint)
    {
        await EnablePushAsync();

        Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync(endpoint)).StatusCode);
    }

    [Fact]
    public async Task Keys_that_are_not_the_shape_a_browser_produces_are_refused()
    {
        await EnablePushAsync();

        var response = await _customer.PostAsJsonAsync("/api/me/push/subscribe", new
        {
            endpoint = Endpoint,
            p256dh = "too-short",
            auth = BrowserAuth,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Subscribing while the shop has push switched off would file a row against
    /// a channel that cannot send, and the customer would be told it worked.
    /// </summary>
    [Fact]
    public async Task A_browser_cannot_subscribe_while_push_is_off()
    {
        Assert.Equal(HttpStatusCode.BadRequest, (await SubscribeAsync()).StatusCode);
    }

    [Fact]
    public async Task A_visitor_with_no_session_cannot_subscribe()
    {
        await EnablePushAsync();

        using var anonymous = _factory.CreateClient();

        var response = await anonymous.PostAsJsonAsync("/api/me/push/subscribe", new
        {
            endpoint = Endpoint,
            p256dh = BrowserKey,
            auth = BrowserAuth,
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    /// <summary>
    /// The endpoint names one browser, so the same one arriving under a second
    /// customer is a shared device. It moves rather than being duplicated — the
    /// person sitting at it now is the one whose news should arrive there.
    /// </summary>
    [Fact]
    public async Task A_shared_browser_moves_to_whoever_subscribed_last()
    {
        await EnablePushAsync();
        (await SubscribeAsync()).EnsureSuccessStatusCode();

        Guid otherId = default;
        await _factory.WithDbAsync(async db =>
            otherId = (await TestData.AddCustomerAsync(db, "09120000002")).Id);

        using var other = _factory.CreateCustomerClient(otherId);

        var response = await other.PostAsJsonAsync("/api/me/push/subscribe", new
        {
            endpoint = Endpoint,
            p256dh = BrowserKey,
            auth = BrowserAuth,
        });

        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var subscription = await db.PushSubscriptions.SingleAsync();
            Assert.Equal(otherId, subscription.CustomerId);
        });
    }

    /// <summary>
    /// Unsubscribing an endpoint belonging to somebody else must not silence
    /// their device. It reports success — the caller's intent is satisfied, they
    /// have no subscription at that endpoint — and changes nothing.
    /// </summary>
    [Fact]
    public async Task One_customer_cannot_unsubscribe_anothers_browser()
    {
        await EnablePushAsync();
        (await SubscribeAsync()).EnsureSuccessStatusCode();

        Guid otherId = default;
        await _factory.WithDbAsync(async db =>
            otherId = (await TestData.AddCustomerAsync(db, "09120000003")).Id);

        using var other = _factory.CreateCustomerClient(otherId);

        var response = await other.PostAsJsonAsync("/api/me/push/unsubscribe", new { endpoint = Endpoint });
        response.EnsureSuccessStatusCode();

        await _factory.WithDbAsync(async db =>
        {
            var subscription = await db.PushSubscriptions.SingleAsync();
            Assert.Equal(_customerId, subscription.CustomerId);
        });
    }

    /// <summary>
    /// RFC 8292 allows only <c>mailto:</c> and <c>https:</c>. A push service that
    /// validates it refuses everything the shop sends, which has no symptom
    /// except notifications that never arrive — so it is caught at the field.
    /// </summary>
    [Fact]
    public async Task A_contact_subject_that_is_not_a_mailto_or_url_is_refused()
    {
        await EnablePushAsync();

        var response = await _owner.PostAsJsonAsync("/api/admin/push/settings", new
        {
            enabled = true,
            subject = "shop@bojan.test",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    /// <summary>
    /// Owner only. Whoever holds the private key can send a notification in the
    /// shop's name to every browser that ever agreed to hear from it.
    /// </summary>
    [Fact]
    public async Task An_operator_who_is_not_the_owner_cannot_read_or_change_the_keys()
    {
        Guid salesId = default;
        await _factory.WithDbAsync(async db =>
            salesId = (await TestData.AddAdminAsync(db, AdminRole.Sales, "sales@example.com")).Id);

        using var sales = _factory.CreateAdminClient(salesId);

        Assert.Equal(HttpStatusCode.Forbidden, (await sales.GetAsync("/api/admin/push/settings")).StatusCode);

        var generated = await sales.PostAsJsonAsync("/api/admin/push/settings/keys", new { confirm = true });
        Assert.Equal(HttpStatusCode.Forbidden, generated.StatusCode);
    }

    /// <summary>
    /// A broadcast on a channel the shop has not configured is refused rather
    /// than queued and dropped at dispatch — the operator being told is worth
    /// more than a row in a table nobody reads.
    /// </summary>
    [Fact]
    public async Task A_push_broadcast_is_refused_while_the_channel_is_off()
    {
        var response = await _owner.PostAsJsonAsync("/api/admin/notifications", new
        {
            channel = "push",
            audience = "all",
            title = "خبر",
            body = "متن",
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task A_push_broadcast_is_accepted_once_the_channel_is_on()
    {
        await EnablePushAsync();

        var response = await _owner.PostAsJsonAsync("/api/admin/notifications", new
        {
            channel = "push",
            audience = "all",
            title = "خبر",
            body = "متن",
        });

        response.EnsureSuccessStatusCode();
    }
}
