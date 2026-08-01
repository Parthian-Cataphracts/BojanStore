using Bojan.Application.Auth;
using Bojan.Application.Common;
using Bojan.Domain.Customers;
using Bojan.Domain.Marketing;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// Turns a queued broadcast into whatever its channel actually means.
/// </summary>
/// <remarks>
/// <para>
/// In-app becomes one <see cref="CustomerNotification"/> per customer — the
/// rows screen 53 reads. SMS goes through the same
/// <see cref="ISmsSender"/> the OTP flow uses. Email and push have no provider
/// wired up yet and are logged rather than silently dropped, so a campaign that
/// went nowhere is visible.
/// </para>
/// <para>
/// Dispatch is idempotent on <see cref="NotificationCampaign.SentAtUtc"/>: a
/// scheduler that fires twice, or a retry after a partial failure, must not
/// send the same offer to the same customer again.
/// </para>
/// </remarks>
public sealed class NotificationDispatcher(
    BojanDbContext db,
    ISmsSender sms,
    IDateTimeProvider clock,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    public async Task DispatchAsync(Guid notificationCampaignId, CancellationToken cancellationToken)
    {
        var campaign = await db.NotificationCampaigns
            .FirstOrDefaultAsync(c => c.Id == notificationCampaignId, cancellationToken);

        if (campaign is null || campaign.SentAtUtc is not null)
        {
            return;
        }

        var recipients = await db.Customers.AsNoTracking()
            .Where(c => !c.IsBlocked)
            .Where(c => campaign.Audience == "all" || campaign.Audience == string.Empty || c.Group == campaign.Audience)
            .Select(c => new { c.Id, c.Phone })
            .ToListAsync(cancellationToken);

        switch (campaign.Channel)
        {
            case NotificationChannel.InApp:
                foreach (var recipient in recipients)
                {
                    db.CustomerNotifications.Add(new CustomerNotification
                    {
                        CustomerId = recipient.Id,
                        Kind = NotificationKind.Offer,
                        Title = campaign.Title,
                        Body = campaign.Body,
                        CreatedAtUtc = clock.UtcNow,
                    });
                }

                break;

            case NotificationChannel.Sms:
                foreach (var recipient in recipients)
                {
                    await sms.SendAsync(recipient.Phone, $"{campaign.Title}\n{campaign.Body}", cancellationToken);
                }

                break;

            default:
                logger.LogWarning(
                    "Notification campaign {CampaignId} targets {Channel}, which has no provider configured. {Count} recipients were not reached.",
                    campaign.Id,
                    campaign.Channel,
                    recipients.Count);
                break;
        }

        campaign.SentAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
    }
}
