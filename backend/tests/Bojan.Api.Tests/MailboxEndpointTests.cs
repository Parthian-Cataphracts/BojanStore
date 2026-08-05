using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Bojan.Domain.Admin;

namespace Bojan.Api.Tests;

/// <summary>
/// The mailbox endpoints, over real HTTP.
/// </summary>
/// <remarks>
/// No IMAP server is involved. What these prove is everything around it: who
/// may reach which route, that an unconfigured mailbox says so instead of
/// failing as a server fault, and that the password never travels outwards.
/// The IMAP conversation itself is MailKit's to get right, and standing up a
/// mail server per test run would prove that rather than any of this.
/// </remarks>
public sealed class MailboxEndpointTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();
    private HttpClient _owner = null!;
    private HttpClient _support = null!;
    private HttpClient _product = null!;

    public async Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();

        Guid ownerId = default;
        Guid supportId = default;
        Guid productId = default;

        await _factory.WithDbAsync(async db =>
        {
            ownerId = (await TestData.AddAdminAsync(db, AdminRole.Owner, "owner@bojan.test")).Id;
            supportId = (await TestData.AddAdminAsync(db, AdminRole.Support, "support@bojan.test")).Id;
            productId = (await TestData.AddAdminAsync(db, AdminRole.Product, "product@bojan.test")).Id;
            await db.SaveChangesAsync();
        });

        _owner = _factory.CreateAdminClient(ownerId);
        _support = _factory.CreateAdminClient(supportId);
        _product = _factory.CreateAdminClient(productId);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose()
    {
        _owner?.Dispose();
        _support?.Dispose();
        _product?.Dispose();
        _factory.Dispose();
    }

    [Fact]
    public async Task An_unconfigured_mailbox_says_so_rather_than_failing()
    {
        var response = await _support.GetAsync("/api/admin/support/mailbox/conversations");

        // 502, not 500: what is unavailable is the mail server this API talks
        // to on the operator's behalf, and the screen shows the sentence.
        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);

        var problem = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("mailbox-unavailable", problem.GetProperty("title").GetString());
        Assert.Contains("پیکربندی", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_unread_badge_answers_zero_rather_than_failing()
    {
        // Rendered beside every screen in the panel. A badge is not worth
        // failing a page over, so an unreachable mailbox counts as none.
        var body = await (await _support.GetAsync("/api/admin/support/mailbox/unread-count"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal(0, body.GetProperty("count").GetInt32());
    }

    [Fact]
    public async Task Support_reads_the_inbox_and_the_catalogue_role_does_not()
    {
        // Support reaches it — 502 because nothing is configured, which is past
        // the gate.
        Assert.Equal(
            HttpStatusCode.BadGateway,
            (await _support.GetAsync("/api/admin/support/mailbox/conversations")).StatusCode);

        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await _product.GetAsync("/api/admin/support/mailbox/conversations")).StatusCode);
    }

    [Fact]
    public async Task Only_the_owner_touches_the_settings()
    {
        // Reading customer mail and holding the credential to the mail account
        // are different levels of trust, so they are different gates.
        Assert.Equal(
            HttpStatusCode.Forbidden,
            (await _support.GetAsync("/api/admin/support/mailbox/settings")).StatusCode);

        (await _owner.GetAsync("/api/admin/support/mailbox/settings")).EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task The_password_is_stored_but_never_returned()
    {
        var save = await _owner.PostAsJsonAsync("/api/admin/support/mailbox/settings", new
        {
            enabled = true,
            imapHost = "imap.example.com",
            imapPort = 993,
            imapUseSsl = true,
            smtpHost = "smtp.example.com",
            smtpPort = 587,
            smtpUseSsl = true,
            username = "support@bojanstore.com",
            password = "a-real-secret",
            address = "support@bojanstore.com",
            displayName = "پشتیبانی بوژان",
        });

        save.EnsureSuccessStatusCode();

        var body = await (await _owner.GetAsync("/api/admin/support/mailbox/settings"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("imap.example.com", body.GetProperty("imapHost").GetString());

        // The flag is how the form knows to say "saved" over an empty box.
        Assert.True(body.GetProperty("hasPassword").GetBoolean());

        // There is no route by which it could come back: the DTO has no field
        // for it at all.
        Assert.False(body.TryGetProperty("password", out _));
        Assert.DoesNotContain("a-real-secret", body.GetRawText(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_absent_password_keeps_the_stored_one()
    {
        await _owner.PostAsJsonAsync("/api/admin/support/mailbox/settings", new
        {
            enabled = true,
            imapHost = "imap.example.com",
            imapPort = 993,
            imapUseSsl = true,
            smtpHost = "smtp.example.com",
            smtpPort = 587,
            smtpUseSsl = true,
            username = "support@bojanstore.com",
            password = "a-real-secret",
            address = "support@bojanstore.com",
            displayName = "پشتیبانی",
        });

        // The form never shows the password, so submitting it with the field
        // empty has to mean "leave it alone" rather than "clear it".
        var again = await _owner.PostAsJsonAsync("/api/admin/support/mailbox/settings", new
        {
            enabled = true,
            imapHost = "imap2.example.com",
            imapPort = 993,
            imapUseSsl = true,
            smtpHost = "smtp.example.com",
            smtpPort = 587,
            smtpUseSsl = true,
            username = "support@bojanstore.com",
            address = "support@bojanstore.com",
            displayName = "پشتیبانی",
        });

        again.EnsureSuccessStatusCode();

        var body = await (await _owner.GetAsync("/api/admin/support/mailbox/settings"))
            .Content.ReadFromJsonAsync<JsonElement>();

        Assert.Equal("imap2.example.com", body.GetProperty("imapHost").GetString());
        Assert.True(body.GetProperty("hasPassword").GetBoolean());
    }

    [Fact]
    public async Task The_password_is_not_stored_in_the_clear()
    {
        await _owner.PostAsJsonAsync("/api/admin/support/mailbox/settings", new
        {
            enabled = true,
            imapHost = "imap.example.com",
            imapPort = 993,
            imapUseSsl = true,
            smtpHost = "smtp.example.com",
            smtpPort = 587,
            smtpUseSsl = true,
            username = "support@bojanstore.com",
            password = "a-real-secret",
            address = "support@bojanstore.com",
            displayName = "پشتیبانی",
        });

        await _factory.WithDbAsync(async db =>
        {
            var rows = db.Settings.Where(entry => entry.Section == "mailbox").ToList();
            var stored = rows.Single(entry => entry.Key == "password").Value;

            // Encrypted rather than hashed — it has to be replayed to an IMAP
            // server — so what must be true is that the table alone does not
            // yield it.
            Assert.NotEqual("a-real-secret", stored);
            Assert.DoesNotContain("a-real-secret", stored, StringComparison.Ordinal);
            Assert.NotEmpty(stored);

            await Task.CompletedTask;
        });
    }

    [Fact]
    public async Task Replying_needs_a_working_mailbox_and_says_when_there_is_none()
    {
        var response = await _support.PostAsJsonAsync("/api/admin/support/mailbox/reply", new
        {
            to = new[] { "customer@example.com" },
            cc = Array.Empty<string>(),
            subject = "Re: سفارش",
            body = "پاسخ آزمایشی",
            replyToFolder = (string?)null,
            inReplyToUid = (uint?)null,
        });

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }

    [Fact]
    public async Task A_malformed_attachment_reference_is_refused_before_any_connection()
    {
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await _support.GetAsync("/api/admin/support/mailbox/attachments/INBOX/5/-1")).StatusCode);
    }
}
