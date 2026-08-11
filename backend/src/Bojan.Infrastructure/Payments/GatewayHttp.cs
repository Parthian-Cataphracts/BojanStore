using System.Net.Http;
using System.Text.Json;

namespace Bojan.Infrastructure.Payments;

/// <summary>
/// Reading a payment provider's answer, for the three adapters that need it.
/// </summary>
/// <remarks>
/// <para>
/// One helper rather than the same eight lines in each gateway, and it exists
/// for a failure the obvious code does not survive: a provider that answers with
/// something other than JSON. IDPay was doing exactly that during this work — a
/// plain <c>502 Bad Gateway</c> HTML page from its edge — and
/// <c>ReadFromJsonAsync</c> meets that with a <see cref="JsonException"/>, which
/// is not what any caller here is written to expect.
/// </para>
/// <para>
/// On the verification path that difference matters. A gateway adapter throws
/// <see cref="InvalidOperationException"/> to mean "nobody could be asked", and
/// the settlement service leaves the order alone. An unhandled
/// <see cref="JsonException"/> would instead reach the callback endpoint as a
/// 500, which tells the shopper the shop is broken when the truth is that their
/// payment provider is having a bad minute.
/// </para>
/// </remarks>
internal static class GatewayHttp
{
    /// <summary>
    /// Reads the response body as a JSON object.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// The body was not a JSON object — an HTML error page, an empty response, a
    /// proxy's plain-text refusal. The provider's name and the status code are
    /// in the message, because that pair is what a person needs to know which
    /// side is unwell.
    /// </exception>
    public static async Task<JsonElement> ReadJsonAsync(
        HttpResponseMessage response,
        string provider,
        CancellationToken cancellationToken)
    {
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        try
        {
            using var document = JsonDocument.Parse(body);

            if (document.RootElement.ValueKind is not JsonValueKind.Object)
            {
                throw new InvalidOperationException(
                    $"{provider} answered {(int)response.StatusCode} with a {document.RootElement.ValueKind} rather than an object.");
            }

            // Cloned because the document is disposed on the way out of this
            // method, and an element outlives its document only as a copy.
            return document.RootElement.Clone();
        }
        catch (JsonException)
        {
            throw new InvalidOperationException(
                $"{provider} answered {(int)response.StatusCode} with a body that is not JSON.");
        }
    }
}
