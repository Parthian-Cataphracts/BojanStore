using System.Security.Cryptography;
using System.Text;

namespace Bojan.Api.Auth;

/// <summary>
/// Comparing a presented secret against a configured one without leaking how
/// long the configured one is.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="CryptographicOperations.FixedTimeEquals"/> is fixed-time over the
/// bytes it compares and returns immediately when the two spans differ in
/// length — it cannot do otherwise, since there is nothing to compare against.
/// Handed the raw UTF-8 of an API key that is what happens: a caller who varies
/// the length of their guess can, in principle, measure which length costs more
/// and learn how long the real key is.
/// </para>
/// <para>
/// Hashing both sides first removes the question. SHA-256 of anything is
/// thirty-two bytes, so every comparison is over the same length and the early
/// return can never fire. The hash is not there to protect the secret — it is
/// already in this process's memory — only to make the two operands the same
/// size.
/// </para>
/// </remarks>
public static class SecretComparison
{
    public static bool Matches(string? presented, string? configured)
    {
        if (string.IsNullOrEmpty(presented) || string.IsNullOrEmpty(configured))
        {
            return false;
        }

        Span<byte> presentedHash = stackalloc byte[32];
        Span<byte> configuredHash = stackalloc byte[32];

        SHA256.HashData(Encoding.UTF8.GetBytes(presented), presentedHash);
        SHA256.HashData(Encoding.UTF8.GetBytes(configured), configuredHash);

        return CryptographicOperations.FixedTimeEquals(presentedHash, configuredHash);
    }
}
