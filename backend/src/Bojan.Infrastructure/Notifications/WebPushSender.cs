using System.Net;
using System.Net.Http;
using System.Text.Json;
using Bojan.Application.Common;
using Bojan.Application.Notifications;
using Bojan.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Notifications;

/// <summary>
/// Delivers one notification to one browser, over the Web Push protocol.
/// </summary>
/// <remarks>
/// <para>
/// No provider and no account: the message goes straight to whichever push
/// service the browser named when it subscribed — Google's for Chrome,
/// Mozilla's for Firefox, Apple's for Safari — and the shop's only credential is
/// a key pair it generated itself. That is why this channel is here rather than
/// behind a paid third party: it costs nothing, it needs no contract, and
/// nothing about the customer leaves the building except a message that only
/// their own browser can decrypt.
/// </para>
/// <para>
/// Failures are swallowed, as with SMS and email: one closed browser must not
/// stop a broadcast reaching the rest of the audience. The one failure acted on
/// is <c>404</c> or <c>410</c>, which is a push service saying the subscription
/// no longer exists — that row is deleted, because retrying it forever is how a
/// subscription table fills with browsers that were uninstalled a year ago.
/// </para>
/// </remarks>
public sealed class WebPushSender(
    IHttpClientFactory httpClientFactory,
    BojanDbContext db,
    WebPushSettingsStore settings,
    IDateTimeProvider clock,
    ILogger<WebPushSender> logger) : IWebPushSender
{
    public const string HttpClientName = "webpush";

    /// <summary>
    /// How long the push service holds a message for a browser that is offline.
    /// </summary>
    /// <remarks>
    /// Four hours. Shop news has a short shelf life — an order that shipped this
    /// morning is not worth waking a phone about on Thursday — and a TTL of zero
    /// would drop anything for a device that happens to be asleep, which is most
    /// of them most of the time.
    /// </remarks>
    private const int TtlSeconds = 4 * 60 * 60;

    public async Task<bool> SendAsync(Guid subscriptionId, PushMessage message, CancellationToken cancellationToken)
    {
        var subscription = await db.PushSubscriptions
            .FirstOrDefaultAsync(row => row.Id == subscriptionId, cancellationToken);

        if (subscription is null)
        {
            return false;
        }

        var (configured, privateKey) = await settings.GetWithPrivateKeyAsync(cancellationToken);

        if (!configured.Enabled || privateKey.Length == 0)
        {
            logger.LogWarning("A push notification was composed while Web Push is not configured.");
            return false;
        }

        if (!Uri.TryCreate(subscription.Endpoint, UriKind.Absolute, out var endpoint))
        {
            return false;
        }

        byte[] body;
        string authorization;

        try
        {
            // The link travels in the payload rather than as a header: the
            // service worker is what opens it, and it only ever sees the
            // decrypted body.
            var payload = JsonSerializer.SerializeToUtf8Bytes(new
            {
                title = message.Title,
                body = message.Body,
                link = message.Link,
            });

            body = WebPushCrypto.Encrypt(
                payload,
                WebPushCrypto.FromBase64Url(subscription.P256dh),
                WebPushCrypto.FromBase64Url(subscription.Auth));

            authorization = WebPushCrypto.VapidHeader(
                endpoint,
                // Falls back to the endpoint's own origin when the owner has not
                // set one. Some push services refuse a message with no subject
                // at all, and refusing to send is worse than a subject that only
                // says where the traffic came from.
                configured.Subject.Length > 0 ? configured.Subject : $"mailto:noreply@{endpoint.Host}",
                WebPushCrypto.FromBase64Url(configured.PublicKey),
                WebPushCrypto.FromBase64Url(privateKey),
                clock.UtcNow);
        }
        catch (Exception exception) when (exception is FormatException or ArgumentException or System.Security.Cryptography.CryptographicException)
        {
            // A subscription whose keys will not decode is one no message can
            // ever reach. Deleted rather than retried on every broadcast.
            logger.LogWarning(exception, "Discarding a push subscription whose keys could not be used.");
            db.PushSubscriptions.Remove(subscription);
            await db.SaveChangesAsync(cancellationToken);
            return false;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new ByteArrayContent(body),
        };

        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        request.Headers.TryAddWithoutValidation("TTL", TtlSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.TryAddWithoutValidation("Urgency", "normal");
        request.Content.Headers.TryAddWithoutValidation("Content-Encoding", "aes128gcm");
        request.Content.Headers.TryAddWithoutValidation("Content-Type", "application/octet-stream");

        var client = httpClientFactory.CreateClient(HttpClientName);

        HttpResponseMessage response;

        try
        {
            response = await client.SendAsync(request, cancellationToken);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
        {
            logger.LogWarning(exception, "A push service could not be reached at {Host}.", endpoint.Host);
            return false;
        }

        using (response)
        {
            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                db.PushSubscriptions.Remove(subscription);
                await db.SaveChangesAsync(cancellationToken);
                return false;
            }

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning(
                    "The push service at {Host} refused a message with {Status}.",
                    endpoint.Host,
                    (int)response.StatusCode);
                return false;
            }
        }

        subscription.LastSentAtUtc = clock.UtcNow;
        await db.SaveChangesAsync(cancellationToken);
        return true;
    }
}
