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
/// rows screen 53 reads. SMS goes out on the shop's own line through the same
/// <see cref="ISmsSender"/> the sign-in codes use, and email through the
/// support mailbox account. Push has no provider and is logged rather than
/// silently dropped, so a campaign that went nowhere is visible.
/// </para>
/// <para>
/// Dispatch is idempotent on <see cref="NotificationCampaign.SentAtUtc"/>: a
/// scheduler that fires twice, or a retry after a partial failure, must not
/// send the same offer to the same customer again. Every channel is also
/// <i>resumable</i>, which is the stronger property and the one that matters
/// once real messages cost money — an attempt that dies on the ten-thousandth
/// recipient carries on from there rather than starting at the first. In-app
/// resumes on the rows it wrote; the two channels that leave nothing behind
/// resume on <see cref="NotificationDelivery"/>.
/// </para>
/// </remarks>
public sealed class NotificationDispatcher(
    BojanDbContext db,
    ISmsSender sms,
    IEmailSender email,
    IDateTimeProvider clock,
    ILogger<NotificationDispatcher> logger) : INotificationDispatcher
{
    /// <summary>How many per-customer rows are written before saving.</summary>
    private const int BatchSize = 500;

    /// <summary>
    /// How many outbound messages go out before the delivery records are
    /// committed.
    /// </summary>
    /// <remarks>
    /// Deliberately far smaller than <see cref="BatchSize"/>. Everything
    /// between one commit and the next is what a crash re-sends, and on these
    /// channels a re-send costs the shop money and costs the customer a
    /// duplicate — so the window is sized to what nobody would notice rather
    /// than to what is efficient.
    /// </remarks>
    private const int DeliveryBatchSize = 25;

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
            .Select(c => new { c.Id, c.Phone, c.Email })
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
                await SendAndRecordAsync(
                    campaign,
                    recipients.Select(r => (r.Id, Address: r.Phone)),
                    (phone, token) => sms.SendAsync(phone, $"{campaign.Title}\n{campaign.Body}", token),
                    cancellationToken);

                break;

            case NotificationChannel.Email:
                // Only the customers who have an address. The shop's main
                // sign-up path is a phone number, so an address is genuinely
                // optional and most audiences are only part covered — which is
                // a skip, not a failure. Same rule ICustomerMailer follows on
                // the transactional path.
                await SendAndRecordAsync(
                    campaign,
                    recipients
                        .Where(r => !string.IsNullOrWhiteSpace(r.Email))
                        .Select(r => (r.Id, Address: r.Email!)),
                    (address, token) => email.SendAsync(address, campaign.Title, campaign.Body, token),
                    cancellationToken);

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

    /// <summary>
    /// Sends to everyone who has not already had it, recording each as it goes.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The record is written <b>after</b> the send, which is the deliberate
    /// choice between the two ways this can be wrong. Recording first means a
    /// crash between the two loses a message nobody knows was lost; recording
    /// after means the same crash re-sends at most one batch. A duplicate is
    /// visible and recoverable and a silent gap is neither — and for an offer,
    /// a customer receiving it twice beats the shop believing it reached
    /// someone it did not.
    /// </para>
    /// <para>
    /// Nothing here throws over one recipient. Both senders swallow their own
    /// failures by contract, and the whole point of the loop is that one dead
    /// address or one blacklisted number does not stop the rest of the audience.
    /// </para>
    /// </remarks>
    private async Task SendAndRecordAsync(
        NotificationCampaign campaign,
        IEnumerable<(Guid Id, string Address)> recipients,
        Func<string, CancellationToken, Task> send,
        CancellationToken cancellationToken)
    {
        var alreadySent = await db.NotificationDeliveries.AsNoTracking()
            .Where(d => d.CampaignId == campaign.Id)
            .Select(d => d.CustomerId)
            .ToHashSetAsync(cancellationToken);

        var pending = recipients.Where(r => !alreadySent.Contains(r.Id)).ToList();

        for (var offset = 0; offset < pending.Count; offset += DeliveryBatchSize)
        {
            foreach (var (customerId, address) in pending.Skip(offset).Take(DeliveryBatchSize))
            {
                await send(address, cancellationToken);

                db.NotificationDeliveries.Add(new NotificationDelivery
                {
                    CampaignId = campaign.Id,
                    CustomerId = customerId,
                    Channel = campaign.Channel,
                    SentAtUtc = clock.UtcNow,
                });
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }
}
