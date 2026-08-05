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
    /// <summary>How many per-customer rows are written before saving.</summary>
    private const int BatchSize = 500;

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
                // Saved in batches rather than accumulated into one change set.
                // A shop with a hundred thousand customers was one `SaveChanges`
                // of a hundred thousand inserts: the tracker holds every entity
                // until it completes, and a failure anywhere loses the lot.
                //
                // Batching alone would trade that for something worse. A batch
                // that fails leaves the earlier ones committed and the campaign
                // unstamped, so the next poll starts again from the top and
                // sends the same offer a second time to everyone the first
                // attempt reached. Skipping who already has it is what makes the
                // retry resume instead of repeat.
                var alreadySent = await db.CustomerNotifications.AsNoTracking()
                    .Where(n => n.CampaignId == campaign.Id)
                    .Select(n => n.CustomerId)
                    .ToHashSetAsync(cancellationToken);

                var pending = recipients.Where(r => !alreadySent.Contains(r.Id)).ToList();

                for (var offset = 0; offset < pending.Count; offset += BatchSize)
                {
                    foreach (var recipient in pending.Skip(offset).Take(BatchSize))
                    {
                        db.CustomerNotifications.Add(new CustomerNotification
                        {
                            CustomerId = recipient.Id,
                            CampaignId = campaign.Id,
                            Kind = NotificationKind.Offer,
                            Title = campaign.Title,
                            Body = campaign.Body,
                            CreatedAtUtc = clock.UtcNow,
                        });
                    }

                    await db.SaveChangesAsync(cancellationToken);
                }

                break;

            case NotificationChannel.Sms:
                // At-least-once, unlike the in-app branch above, and knowingly
                // so: an SMS leaves no row behind, so a failure part-way through
                // cannot be resumed and the retry starts from the first
                // recipient. With ConsoleSmsSender that costs a duplicate log
                // line. With a real gateway it would cost money and a duplicate
                // message, and the fix is a per-recipient delivery record —
                // which belongs with the gateway work, not ahead of it.
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
