using Bojan.Application.Common;
using Bojan.Application.Contracts;
using Bojan.Domain.Customers;

namespace Bojan.Application.Notifications;

/// <summary>
/// A customer switching browser notifications on or off, from their own device.
/// </summary>
/// <remarks>
/// <para>
/// The browser does the agreeing — the permission prompt is the operating
/// system's and nothing here can bypass it. By the time this is called the
/// customer has already said yes and the browser has minted an endpoint; all
/// this does is remember where to reach them.
/// </para>
/// <para>
/// Signed in only. A subscription with no customer attached is a browser
/// nothing can address: broadcasts go to an audience of customers and every
/// transactional message is about somebody's own order.
/// </para>
/// </remarks>
public sealed class PushSubscriptionService(
    IPushSubscriptionRepository repository,
    IWebPushSettingsStore settings,
    IUnitOfWork unitOfWork,
    IDateTimeProvider clock)
{
    /// <summary>
    /// How many browsers one customer may register.
    /// </summary>
    /// <remarks>
    /// A person has a phone, a laptop and maybe a tablet. The ceiling is not
    /// about those — it is that the endpoint changes whenever a browser clears
    /// its site data, so a customer who does that weekly would otherwise
    /// accumulate rows forever, and every one of them costs an HTTP request on
    /// every broadcast. Past the limit the oldest goes.
    /// </remarks>
    private const int MaxSubscriptionsPerCustomer = 10;

    private const int MaxEndpointLength = 1000;

    public async Task<PushAvailabilityDto> GetAvailabilityAsync(CancellationToken cancellationToken)
    {
        var configured = await settings.GetAsync(cancellationToken);

        // The public key only when push is actually usable. Handing one over
        // while the private half is missing produces browsers subscribed to a
        // shop that can never sign a message to them.
        return configured.Enabled
            ? new PushAvailabilityDto(true, configured.PublicKey)
            : new PushAvailabilityDto(false, string.Empty);
    }

    public async Task<UseCaseResult> SubscribeAsync(
        Guid customerId,
        SavePushSubscriptionRequest request,
        string? userAgent,
        CancellationToken cancellationToken)
    {
        var configured = await settings.GetAsync(cancellationToken);

        if (!configured.Enabled)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "push-unavailable");
        }

        var endpoint = request.Endpoint.Trim();

        // An endpoint has to be an absolute HTTPS URL at the browser's own push
        // service. Anything else is either a broken client or an attempt to make
        // the shop's server issue signed requests at a host of someone else's
        // choosing, which is the interesting half of that sentence.
        if (endpoint.Length is 0 or > MaxEndpointLength ||
            !Uri.TryCreate(endpoint, UriKind.Absolute, out var parsed) ||
            parsed.Scheme != Uri.UriSchemeHttps)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "endpoint");
        }

        var p256dh = request.P256dh.Trim();
        var auth = request.Auth.Trim();

        // Lengths as the browser produces them: an uncompressed P-256 point is
        // 65 bytes and the auth secret is 16, which are 87 and 22 characters of
        // unpadded base64url. Checked as a range rather than exactly, since a
        // client that pads is not wrong.
        if (p256dh.Length is < 80 or > 100 || auth.Length is < 20 or > 30)
        {
            return UseCaseResult.Failure(UseCaseError.Invalid, "keys");
        }

        var existing = await repository.FindByEndpointAsync(endpoint, cancellationToken);

        if (existing is not null)
        {
            // The same browser re-subscribing. Its keys rotate when it renews,
            // so they are updated rather than left — and if the row belonged to
            // somebody else, it moves: an endpoint names one browser, and the
            // person sitting at it now is the one whose news should arrive there.
            if (existing.CustomerId != customerId)
            {
                repository.Remove(existing);
            }
            else
            {
                existing.P256dh = p256dh;
                existing.Auth = auth;
                existing.UserAgent = Truncate(userAgent);

                await unitOfWork.SaveChangesAsync(cancellationToken);
                return UseCaseResult.Success();
            }
        }

        var mine = await repository.ListForCustomerAsync(customerId, cancellationToken);

        foreach (var stale in mine.OrderByDescending(row => row.CreatedAtUtc).Skip(MaxSubscriptionsPerCustomer - 1))
        {
            repository.Remove(stale);
        }

        repository.Add(new PushSubscription
        {
            CustomerId = customerId,
            Endpoint = endpoint,
            P256dh = p256dh,
            Auth = auth,
            UserAgent = Truncate(userAgent),
            CreatedAtUtc = clock.UtcNow,
        });

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return UseCaseResult.Success();
    }

    /// <summary>
    /// Forgets one browser.
    /// </summary>
    /// <remarks>
    /// Succeeds when there was nothing to forget. The customer's intent is "do
    /// not send here", and a browser that already unsubscribed itself has the
    /// same outcome — reporting that as a failure would only invite the page to
    /// retry.
    /// </remarks>
    public async Task<UseCaseResult> UnsubscribeAsync(
        Guid customerId,
        string endpoint,
        CancellationToken cancellationToken)
    {
        var existing = await repository.FindByEndpointAsync(endpoint.Trim(), cancellationToken);

        if (existing is null || existing.CustomerId != customerId)
        {
            return UseCaseResult.Success();
        }

        repository.Remove(existing);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return UseCaseResult.Success();
    }

    private static string? Truncate(string? userAgent) =>
        userAgent is null || userAgent.Length <= 400 ? userAgent : userAgent[..400];
}

/// <summary>Where the subscriptions live.</summary>
public interface IPushSubscriptionRepository
{
    Task<PushSubscription?> FindByEndpointAsync(string endpoint, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushSubscription>> ListForCustomerAsync(Guid customerId, CancellationToken cancellationToken);

    void Add(PushSubscription subscription);

    void Remove(PushSubscription subscription);
}
