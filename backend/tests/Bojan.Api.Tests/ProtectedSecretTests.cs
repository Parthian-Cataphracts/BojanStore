using Bojan.Application.Contracts;
using Bojan.Infrastructure.Notifications;
using Bojan.Infrastructure.Support;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Bojan.Api.Tests;

/// <summary>
/// A stored credential that can no longer be decrypted must read as absent.
/// </summary>
/// <remarks>
/// <para>
/// This is the shape of a failure that reached production. The SMS API key was
/// sealed by a data-protection key ring living inside the container; the
/// container was rebuilt to move that key ring onto a volume, and the key that
/// could open the stored value went with the old layer. Every field the panel
/// showed was correct — provider, line number, template id — and the API key
/// row was still full of bytes, so the settings screen reported a configured
/// account. Sign-in codes were dropped before a request was ever made to
/// SMS.ir, and the only trace was one log line.
/// </para>
/// <para>
/// The two key rings here are what a rebuild does, expressed as a test: seal
/// with one, read with another. There is no way to fake the failure more
/// honestly than to actually cause it.
/// </para>
/// </remarks>
public sealed class ProtectedSecretTests : IAsyncLifetime, IDisposable
{
    private readonly BojanApiFactory _factory = new();

    public Task InitializeAsync()
    {
        _factory.EnsureDatabaseCreated();
        return Task.CompletedTask;
    }

    public Task DisposeAsync() => Task.CompletedTask;

    public void Dispose() => _factory.Dispose();

    /// <summary>Two processes that never shared a key ring, which is what a rebuild leaves behind.</summary>
    private static IDataProtectionProvider KeyRing() =>
        DataProtectionProvider.Create(new DirectoryInfo(
            Path.Combine(Path.GetTempPath(), "bojan-keyring-test", Guid.NewGuid().ToString("N"))));

    /// <summary>
    /// The one thing an operator cannot see is said in the log, by name.
    /// </summary>
    /// <remarks>
    /// Reporting an unreadable key as "not configured" is the right outcome,
    /// but the sentence that went with it was wrong: "no SMS provider is
    /// configured" to someone who has just watched the key field fill with
    /// dots. This asserts the line that now accompanies it — that the secret
    /// is present, that it was sealed by a key this server lacks, and which
    /// screen to re-enter it on — so the next person reads the log and goes
    /// straight to the panel rather than to the firewall.
    /// </remarks>
    [Fact]
    public async Task An_unreadable_key_is_named_in_the_log_rather_than_reported_as_absent()
    {
        var sealing = KeyRing();
        var reading = KeyRing();
        var log = new CapturingLogger<SmsSettingsStore>();

        await _factory.WithDbAsync(async db =>
        {
            var clock = new Bojan.Infrastructure.Common.SystemDateTimeProvider();

            await new SmsSettingsStore(db, sealing, clock, NullLogger<SmsSettingsStore>.Instance).SaveAsync(
                new SmsSettingsDto("smsir", HasApiKey: true, "30002108032208", 204758, "CODE"),
                "a-real-api-key",
                CancellationToken.None);

            await new SmsSettingsStore(db, reading, clock, log).GetAsync(CancellationToken.None);
        });

        var warning = Assert.Single(log.Entries, entry => entry.Level == LogLevel.Warning);
        Assert.Contains("cannot be decrypted", warning.Message);
        Assert.Contains("SMS.ir API key", warning.Message);
        Assert.Contains("تنظیمات ← پیامک", warning.Message);
    }

    /// <summary>
    /// And nothing is said when the key opens — the warning is for the broken
    /// case only, not a line on every read.
    /// </summary>
    [Fact]
    public async Task A_readable_key_leaves_the_log_alone()
    {
        var ring = KeyRing();
        var log = new CapturingLogger<SmsSettingsStore>();

        await _factory.WithDbAsync(async db =>
        {
            var clock = new Bojan.Infrastructure.Common.SystemDateTimeProvider();

            await new SmsSettingsStore(db, ring, clock, NullLogger<SmsSettingsStore>.Instance).SaveAsync(
                new SmsSettingsDto("smsir", HasApiKey: true, "30002108032208", 204758, "CODE"),
                "a-real-api-key",
                CancellationToken.None);

            var read = await new SmsSettingsStore(db, ring, clock, log).GetAsync(CancellationToken.None);
            Assert.True(read.HasApiKey);
        });

        Assert.Empty(log.Entries);
    }

    /// <summary>Collects what a store logs, so a test can read it back.</summary>
    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) =>
            Entries.Add((logLevel, formatter(state, exception)));
    }

    [Fact]
    public async Task An_sms_key_sealed_by_a_lost_key_ring_reports_as_not_configured()
    {
        var sealing = KeyRing();
        var reading = KeyRing();

        await _factory.WithDbAsync(async db =>
        {
            var clock = new Bojan.Infrastructure.Common.SystemDateTimeProvider();

            await new SmsSettingsStore(db, sealing, clock, NullLogger<SmsSettingsStore>.Instance).SaveAsync(
                new SmsSettingsDto("smsir", HasApiKey: true, "30002108032208", 204758, "CODE"),
                "a-real-api-key",
                CancellationToken.None);

            // Sealed and read back by the same key ring: the ordinary case, and
            // the control for the assertion below.
            var same = await new SmsSettingsStore(db, sealing, clock, NullLogger<SmsSettingsStore>.Instance).GetAsync(CancellationToken.None);
            Assert.True(same.HasApiKey);

            var other = await new SmsSettingsStore(db, reading, clock, NullLogger<SmsSettingsStore>.Instance).GetAsync(CancellationToken.None);

            // The row is untouched and still full of ciphertext; what changed is
            // that nothing can open it, and that is what the panel must say.
            Assert.False(other.HasApiKey);

            // Everything that is not a secret survives, which is exactly why the
            // screen looked healthy.
            Assert.Equal("smsir", other.Provider);
            Assert.Equal(204758, other.OtpTemplateId);
        });
    }

    [Fact]
    public async Task A_mailbox_password_sealed_by_a_lost_key_ring_reports_as_not_configured()
    {
        var sealing = KeyRing();
        var reading = KeyRing();

        await _factory.WithDbAsync(async db =>
        {
            var clock = new Bojan.Infrastructure.Common.SystemDateTimeProvider();

            var settings = new MailboxSettingsDto(
                Enabled: true,
                ImapHost: "mail.bojanstore.com",
                ImapPort: 993,
                ImapUseSsl: true,
                SmtpHost: "mail.bojanstore.com",
                SmtpPort: 587,
                SmtpUseSsl: true,
                Username: "support",
                HasPassword: true,
                Address: "support@bojanstore.com",
                DisplayName: "پشتیبانی بوژان");

            await new MailboxSettingsStore(db, sealing, clock, NullLogger<MailboxSettingsStore>.Instance)
                .SaveAsync(settings, "a-real-password", CancellationToken.None);

            Assert.True((await new MailboxSettingsStore(db, sealing, clock, NullLogger<MailboxSettingsStore>.Instance)
                .GetAsync(CancellationToken.None)).HasPassword);

            Assert.False((await new MailboxSettingsStore(db, reading, clock, NullLogger<MailboxSettingsStore>.Instance)
                .GetAsync(CancellationToken.None)).HasPassword);
        });
    }

    /// <summary>
    /// The health board's own question, which has to give the same answer —
    /// it is the screen an operator checks when mail stops arriving.
    /// </summary>
    [Fact]
    public async Task Send_readiness_treats_an_unreadable_password_as_no_password()
    {
        var sealing = KeyRing();
        var reading = KeyRing();

        await _factory.WithDbAsync(async db =>
        {
            var clock = new Bojan.Infrastructure.Common.SystemDateTimeProvider();

            var settings = new MailboxSettingsDto(
                Enabled: true,
                ImapHost: "mail.bojanstore.com",
                ImapPort: 993,
                ImapUseSsl: true,
                SmtpHost: "mail.bojanstore.com",
                SmtpPort: 587,
                SmtpUseSsl: true,
                Username: "support",
                HasPassword: true,
                Address: "support@bojanstore.com",
                DisplayName: "پشتیبانی بوژان");

            await new MailboxSettingsStore(db, sealing, clock, NullLogger<MailboxSettingsStore>.Instance)
                .SaveAsync(settings, "a-real-password", CancellationToken.None);

            var readiness = await new MailboxSettingsStore(db, reading, clock, NullLogger<MailboxSettingsStore>.Instance)
                .GetSendReadinessAsync(CancellationToken.None);

            Assert.False(readiness.HasPassword);
            Assert.False(readiness.IsReady);

            // The other three still pass, so the board names the password as the
            // one thing to fix rather than reporting a vague outage.
            Assert.True(readiness.Enabled);
            Assert.True(readiness.HasSmtpHost);
            Assert.True(readiness.HasSenderAddress);
        });
    }
}
