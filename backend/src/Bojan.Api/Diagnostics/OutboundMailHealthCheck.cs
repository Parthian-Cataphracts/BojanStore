using Bojan.Infrastructure.Support;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Bojan.Api.Diagnostics;

/// <summary>
/// Whether anything the shop writes actually leaves the building.
/// </summary>
/// <remarks>
/// <para>
/// <c>SmtpEmailSender</c> falls back to <c>ConsoleEmailSender</c> when the
/// mailbox is off or half-filled-in, and that fallback is right: a shop still
/// being set up has to be able to take an order, and failing the order because
/// the receipt could not be mailed would be the wrong trade. What it is not is
/// visible. Order confirmations, password resets and the link to a finished
/// report export were all being written to the log and nowhere else, and every
/// screen that triggers one said it had succeeded — because it had, up to the
/// point where the message needed a mail server.
/// </para>
/// <para>
/// So the state gets a name on the one screen an owner opens to ask why
/// something is not working. Degraded rather than down: the shop is running,
/// and the missing piece is a form somebody has not filled in yet, not a
/// fault. The description says which form.
/// </para>
/// <para>
/// Only the presence of the settings is checked, never a connection. A health
/// board that opens an SMTP session on every poll is a health board that gets
/// the shop's account rate-limited by its own mail provider — and "does this
/// account work" already has a button on the settings screen, pressed by a
/// person who is expecting to wait.
/// </para>
/// </remarks>
public sealed class OutboundMailHealthCheck(MailboxSettingsStore mailbox) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var readiness = await mailbox.GetSendReadinessAsync(cancellationToken);

        if (!readiness.Enabled)
        {
            return HealthCheckResult.Degraded(
                "صندوق پستی خاموش است، پس هیچ ایمیلی ارسال نمی‌شود — نه تأیید سفارش، نه بازیابی گذرواژه، نه لینک خروجی گزارش‌ها. تنظیمات ← صندوق پستی.");
        }

        // Each one on its own line rather than a single "not configured": an
        // owner who has typed four of five fields needs to be told which one is
        // missing, not that the form is incomplete.
        var missing = new List<string>();
        if (!readiness.HasSmtpHost) missing.Add("سرور SMTP");
        if (!readiness.HasSenderAddress) missing.Add("آدرس فرستنده");
        if (!readiness.HasPassword) missing.Add("گذرواژه");

        if (missing.Count > 0)
        {
            return HealthCheckResult.Degraded(
                $"صندوق پستی روشن است ولی {string.Join(" و ", missing)} وارد نشده، پس ایمیل‌ها ارسال نمی‌شوند. تنظیمات ← صندوق پستی.");
        }

        return HealthCheckResult.Healthy();
    }
}
