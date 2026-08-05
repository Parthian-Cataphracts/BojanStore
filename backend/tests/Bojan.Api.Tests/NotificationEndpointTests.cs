using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;
using Bojan.Application.Common;
using Bojan.Domain.Customers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Bojan.Api.Tests;

/// <summary>
/// The notification path, over real HTTP.
/// </summary>
/// <remarks>
/// Every one of these covers something that was broken or unguarded: the
/// channel the composer sent reached nobody, a channel with no provider was
/// accepted and silently dropped, the feed was unbounded, there was no count to
/// put on a badge, an operator could not address one customer, and nothing
/// stopped a link that left the site.
/// </remarks>
public sealed class NotificationEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _admin = null!;
    private HttpClient _customer = null!;
    private Guid _customerId;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;

        await _factory.WithDbAsync(async db =>
        {
            var customer = await TestData.AddCustomerAsync(db, "09121110060");
            _customerId = customer.Id;

            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
            await db.SaveChangesAsync();
        });

        _admin = _factory.CreateAdminClient(ownerId);
        _customer = _factory.CreateCustomerClient(_customerId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _admin?.Dispose();
        _customer?.Dispose();
        _factory.Dispose();
    }

    private async Task<JsonElement> FeedAsync() =>
        await (await _customer.GetAsync("/api/me/notifications")).Content.ReadFromJsonAsync<JsonElement>();

    private async Task<int> UnreadAsync()
    {
        var body = await (await _customer.GetAsync("/api/me/notifications/unread-count"))
            .Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("count").GetInt32();
    }

    [Fact]
    public async Task An_operator_can_notify_one_customer()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/customers/notify", new
        {
            customerId = _customerId.ToString(),
            title = "درباره سفارش شما",
            body = "بسته امروز ارسال شد.",
            link = "/account/orders",
        });

        response.EnsureSuccessStatusCode();

        var feed = await FeedAsync();
        Assert.Equal(1, feed.GetArrayLength());
        Assert.Equal("درباره سفارش شما", feed[0].GetProperty("title").GetString());
        Assert.Equal("/account/orders", feed[0].GetProperty("href").GetString());
        Assert.False(feed[0].GetProperty("read").GetBoolean());

        Assert.Equal(1, await UnreadAsync());
    }

    [Theory]
    [InlineData("https://evil.example")]
    [InlineData("//evil.example")]
    [InlineData("javascript:alert(1)")]
    [InlineData("/\\evil.example")]
    public async Task A_link_that_leaves_the_site_is_refused(string link)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/customers/notify", new
        {
            customerId = _customerId.ToString(),
            title = "عنوان",
            body = "متن",
            link,
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);

        // Refused outright, not stored with the link stripped — an operator who
        // meant to link somewhere should be told, not quietly half-obeyed.
        Assert.Equal(0, (await FeedAsync()).GetArrayLength());
    }

    [Fact]
    public async Task Notifying_an_unknown_customer_is_a_404()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/customers/notify", new
        {
            customerId = Guid.NewGuid().ToString(),
            title = "عنوان",
            body = "متن",
            link = (string?)null,
        });

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task An_in_app_broadcast_is_queued_for_the_worker()
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/notifications", new
        {
            channel = "in-app",
            audience = "all",
            title = "جشنواره بهاره",
            body = "تخفیف ویژه این هفته.",
        });

        response.EnsureSuccessStatusCode();

        // Queued, not delivered inside the request: the fan-out is the worker's,
        // so the campaign exists and is not yet stamped sent.
        await _factory.WithDbAsync(async db =>
        {
            var campaign = await Task.FromResult(db.NotificationCampaigns.Single());
            Assert.Equal("جشنواره بهاره", campaign.Title);
            Assert.Null(campaign.SentAtUtc);
        });
    }

    [Theory]
    [InlineData("email")]
    [InlineData("push")]
    public async Task A_channel_with_no_provider_is_refused_rather_than_queued(string channel)
    {
        var response = await _admin.PostAsJsonAsync("/api/admin/notifications", new
        {
            channel,
            audience = "all",
            title = "عنوان",
            body = "متن",
        });

        // It used to be accepted, stored, and dropped at dispatch with a log
        // line — the panel reported it sent and nobody was reached.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task The_feed_is_capped_and_the_cap_can_be_raised_but_not_past_the_ceiling()
    {
        await _factory.WithDbAsync(async db =>
        {
            for (var index = 0; index < 60; index++)
            {
                db.CustomerNotifications.Add(new CustomerNotification
                {
                    CustomerId = _customerId,
                    Kind = NotificationKind.Offer,
                    Title = $"اعلان {index}",
                    Body = "متن",
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index),
                });
            }

            await db.SaveChangesAsync();
        });

        Assert.Equal(50, (await FeedAsync()).GetArrayLength());

        var raised = await (await _customer.GetAsync("/api/me/notifications?limit=60"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(60, raised.GetArrayLength());

        // Clamped, not trusted — it arrives in a query string.
        var absurd = await (await _customer.GetAsync("/api/me/notifications?limit=100000"))
            .Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(60, absurd.GetArrayLength());

        // Newest first, so the cap keeps the ones worth showing.
        Assert.Equal("اعلان 0", (await FeedAsync())[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Marking_all_read_clears_more_than_the_capped_page()
    {
        await _factory.WithDbAsync(async db =>
        {
            for (var index = 0; index < 60; index++)
            {
                db.CustomerNotifications.Add(new CustomerNotification
                {
                    CustomerId = _customerId,
                    Kind = NotificationKind.Offer,
                    Title = $"اعلان {index}",
                    Body = "متن",
                    CreatedAtUtc = DateTimeOffset.UtcNow.AddMinutes(-index),
                });
            }

            await db.SaveChangesAsync();
        });

        Assert.Equal(60, await UnreadAsync());

        // No ids at all, which is what screen 53's header action posts. Sending
        // the loaded ids instead would leave the ten the feed never returned.
        var response = await _customer.PostAsJsonAsync("/api/me/notifications/read", new { ids = Array.Empty<string>() });
        response.EnsureSuccessStatusCode();

        Assert.Equal(0, await UnreadAsync());
    }

    [Fact]
    public async Task One_customers_notifications_are_not_another_customers()
    {
        Guid strangerId = default;
        await _factory.WithDbAsync(async db =>
        {
            strangerId = (await TestData.AddCustomerAsync(db, "09121110061")).Id;
            await db.SaveChangesAsync();
        });

        await _admin.PostAsJsonAsync("/api/admin/customers/notify", new
        {
            customerId = _customerId.ToString(),
            title = "خصوصی",
            body = "متن",
            link = (string?)null,
        });

        using var stranger = _factory.CreateCustomerClient(strangerId);
        var body = await (await stranger.GetAsync("/api/me/notifications")).Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetArrayLength());
    }

    [Fact]
    public async Task Dispatching_a_campaign_twice_delivers_it_once()
    {
        Guid campaignId = default;

        await _factory.WithDbAsync(async db =>
        {
            var campaign = new Domain.Marketing.NotificationCampaign
            {
                Channel = Domain.Marketing.NotificationChannel.InApp,
                Audience = "all",
                Title = "پیشنهاد ویژه",
                Body = "متن",
                ActorId = Guid.NewGuid(),
            };

            db.NotificationCampaigns.Add(campaign);
            await db.SaveChangesAsync();
            campaignId = campaign.Id;
        });

        // Twice, as an overlapping poll or a retry after a partial failure
        // would. The second must resume, not repeat: without the campaign id on
        // the row there is nothing to tell the dispatcher who already has it,
        // and every customer the first pass reached gets the offer again.
        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
            await dispatcher.DispatchAsync(campaignId, CancellationToken.None);
        }

        // Unstamped, so the second call cannot take the "already sent" exit and
        // has to be stopped by the per-customer check instead.
        await _factory.WithDbAsync(async db =>
        {
            var campaign = await db.NotificationCampaigns.FirstAsync(c => c.Id == campaignId);
            campaign.SentAtUtc = null;
            await db.SaveChangesAsync();
        });

        using (var scope = _factory.Services.CreateScope())
        {
            var dispatcher = scope.ServiceProvider.GetRequiredService<INotificationDispatcher>();
            await dispatcher.DispatchAsync(campaignId, CancellationToken.None);
        }

        var feed = await FeedAsync();
        Assert.Equal(1, feed.EnumerateArray().Count(n => n.GetProperty("title").GetString() == "پیشنهاد ویژه"));
    }

    [Fact]
    public async Task A_query_parameter_that_will_not_parse_is_the_callers_fault()
    {
        // Not a 500. The framework throws BadHttpRequestException, which carries
        // its own 400, and the exception handler reported every one of them as a
        // server error — so `?page=abc` against any paged list in the panel
        // counted against the error budget as an outage.
        var response = await _customer.GetAsync("/api/me/notifications?limit=abc");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }
}
