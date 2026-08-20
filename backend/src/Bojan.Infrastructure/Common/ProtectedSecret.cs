using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;

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
    public static string UnprotectOrEmpty(this IDataProtector protector, string sealedValue)
    {
        if (sealedValue.Length == 0)
        {
            return string.Empty;
        }

        try
        {
            return protector.Unprotect(sealedValue);
        }
        catch (CryptographicException)
        {
            return string.Empty;
        }
    }
}
