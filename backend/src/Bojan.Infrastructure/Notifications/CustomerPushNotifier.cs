using Bojan.Application.Notifications;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// Tells one customer's browsers about something that happened to their own
/// order.
/// </summary>
/// <remarks>
/// <para>
/// The transactional half of push, beside <c>CustomerMailer</c>. A broadcast
/// goes through the dispatcher and is resumable across tens of thousands of
/// recipients; this is one person and at most a handful of devices, so it is
/// simply a loop.
/// </para>
/// <para>
/// Never throws. Every caller is on a path where the work is already done and
/// committed — the order has shipped whether or not a phone lights up — and a
/// notification that fails the request it followed would be strictly worse than
/// no notification.
/// </para>
/// </remarks>
public sealed class CustomerPushNotifier(
    BojanDbContext db,
    IWebPushSender sender,
    IWebPushSettingsStore settings,
    ILogger<CustomerPushNotifier> logger) : ICustomerPushNotifier
{
    public async Task NotifyAsync(Guid customerId, PushMessage message, CancellationToken cancellationToken)
    {
        try
        {
            // Asked before the query rather than after: a shop with push
            // switched off is the common case, and it should cost nothing.
            if (!(await settings.GetAsync(cancellationToken)).Enabled)
            {
                return;
            }

            var subscriptions = await db.PushSubscriptions.AsNoTracking()
                .Where(subscription => subscription.CustomerId == customerId)
                .Select(subscription => subscription.Id)
                .ToListAsync(cancellationToken);

            foreach (var subscriptionId in subscriptions)
            {
                await sender.SendAsync(subscriptionId, message, cancellationToken);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "A push notification for customer {CustomerId} was not delivered.", customerId);
        }
    }
}
