using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Logging;

namespace Bojan.Infrastructure.Common;

/// <summary>
/// Reading back a secret this application sealed earlier.
/// </summary>
/// <remarks>
/// <para>
/// Four settings sections store an encrypted credential — the SMS API key, the
/// mailbox password, the payment merchant id, the Web Push signing key — and
/// each one has to answer the same two questions: what is the value, and is
/// there a usable one at all. They answered the second by measuring the stored
/// ciphertext, which is not the same question: a key ring that was rotated or
/// lost leaves a row full of bytes that no longer decrypt to anything.
/// </para>
/// <para>
/// That gap was not theoretical. The SMS key was sealed by a key ring living
/// inside the container, the container was rebuilt to give the key ring a
/// volume, and the key that could open it went with the old layer. The panel
/// went on reporting a configured SMS account — the ciphertext was still
/// there — while every sign-in code was dropped before a request was made,
/// with the failure visible only as one log line nobody had reason to read.
/// </para>
/// <para>
/// So "is one stored" is answered by opening it. A secret that cannot be read
/// back is not configured, whatever the row says.
/// </para>
/// </remarks>
internal static class ProtectedSecret
{
    /// <summary>
    /// The plaintext, or empty when nothing is stored or the key ring that
    /// sealed it is gone.
    /// </summary>
    /// <remarks>
    /// The two cases are deliberately one outcome. Every caller does the same
    /// thing with them — refuse to send, and tell the operator to enter the
    /// credential again — because entering it again is the only repair for
    /// either, and the second case cannot be distinguished from the first by
    /// anything the operator can see.
    /// </remarks>
    public static string UnprotectOrEmpty(this IDataProtector protector, string sealedValue) =>
        protector.UnprotectOrEmpty(sealedValue, logger: null, what: null);

    /// <summary>
    /// The same, with a line in the log when the stored secret will not open.
    /// </summary>
    /// <remarks>
    /// <para>
    /// The two cases still come out the same — empty — and every caller still
    /// does the same thing with them. What changes is what the log says while
    /// they do it. Downstream, an unreadable key is reported as the provider
    /// being unconfigured, and an operator who has just watched the key field
    /// fill with dots reads that as a lie and looks everywhere except at the
    /// key. That reading cost most of a day once: the SMS key was sealed by a
    /// key ring the server no longer had, the panel showed it present, the log
    /// said "no provider configured", and the network, the firewall, SMS.ir's
    /// IP list and the template were each ruled out in turn before the blob
    /// was decoded by hand and found to name a key that was not on disk.
    /// </para>
    /// <para>
    /// So the one thing the operator cannot see is now said outright, at the
    /// moment it is discovered, with the only repair there is. It is a warning
    /// rather than an error because the callers already raise the error for
    /// the message they then drop — this is the sentence that explains it.
    /// </para>
    /// </remarks>
    /// <param name="what">
    /// What the secret is, for the sentence — "the SMS.ir API key", "the
    /// mailbox password" — so the line names the screen to go to.
    /// </param>
    public static string UnprotectOrEmpty(
        this IDataProtector protector,
        string sealedValue,
        ILogger? logger,
        string? what)
    {
        if (sealedValue.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return protector.Unprotect(sealedValue);
        }
        catch (CryptographicException exception)
        {
            logger?.LogWarning(
                exception,
                "A stored secret ({What}) is present but cannot be decrypted: it was sealed by a data-protection "
                + "key this server does not have. Until it is entered again in the panel it will be treated as "
                + "not configured, and anything that depends on it will be refused.",
                what ?? "unknown");

            return string.Empty;
        }
    }
}
