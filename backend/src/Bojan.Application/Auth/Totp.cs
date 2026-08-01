using System.Security.Cryptography;
using System.Text;

namespace Bojan.Application.Auth;

/// <summary>
/// RFC 6238 time-based one-time passwords — the second factor screen 153 sets
/// up.
/// </summary>
/// <remarks>
/// Written out rather than taken from a package because it is thirty lines of
/// HMAC and a base32 decoder, and because a dependency that computes
/// authentication codes is one more thing to audit. The parameters are the ones
/// every authenticator app assumes without being told: SHA-1, six digits, a
/// thirty-second step.
/// </remarks>
public static class Totp
{
    private const int Digits = 6;
    private static readonly TimeSpan Step = TimeSpan.FromSeconds(30);

    /// <summary>One step either side, so a code entered as the window turns over still verifies.</summary>
    private const int DriftSteps = 1;

    public static string GenerateSecret()
    {
        // 20 bytes is the RFC's recommendation for SHA-1, and encodes to 32
        // base32 characters with no padding.
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var bytes = RandomNumberGenerator.GetBytes(20);
        var builder = new StringBuilder(32);

        for (var index = 0; index < bytes.Length; index += 5)
        {
            var chunk = 0UL;
            var bits = 0;

            for (var offset = 0; offset < 5 && index + offset < bytes.Length; offset++)
            {
                chunk = (chunk << 8) | bytes[index + offset];
                bits += 8;
            }

            chunk <<= 40 - bits;

            for (var position = 0; position < (bits + 4) / 5; position++)
            {
                builder.Append(alphabet[(int)((chunk >> (35 - (position * 5))) & 0x1F)]);
            }
        }

        return builder.ToString();
    }

    public static bool Verify(string base32Secret, string code, DateTimeOffset nowUtc)
    {
        if (code.Length != Digits || !code.All(char.IsAsciiDigit))
        {
            return false;
        }

        byte[] key;
        try
        {
            key = FromBase32(base32Secret);
        }
        catch (FormatException)
        {
            return false;
        }

        var counter = nowUtc.ToUnixTimeSeconds() / (long)Step.TotalSeconds;

        for (var drift = -DriftSteps; drift <= DriftSteps; drift++)
        {
            // Fixed-time comparison: a code check that returns early on the
            // first wrong digit leaks how much of a guess was right.
            if (CryptographicOperations.FixedTimeEquals(
                    Encoding.ASCII.GetBytes(Compute(key, counter + drift)),
                    Encoding.ASCII.GetBytes(code)))
            {
                return true;
            }
        }

        return false;
    }

    private static string Compute(byte[] key, long counter)
    {
        Span<byte> message = stackalloc byte[8];
        for (var index = 7; index >= 0; index--)
        {
            message[index] = (byte)(counter & 0xFF);
            counter >>= 8;
        }

        Span<byte> hash = stackalloc byte[20];
        HMACSHA1.HashData(key, message, hash);

        var offset = hash[^1] & 0x0F;
        var binary = ((hash[offset] & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) << 8)
            | (hash[offset + 3] & 0xFF);

        return (binary % 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
    }

    private static byte[] FromBase32(string value)
    {
        const string alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";
        var cleaned = value.TrimEnd('=').ToUpperInvariant().Replace(" ", string.Empty);
        var output = new List<byte>(cleaned.Length * 5 / 8);

        var buffer = 0;
        var bits = 0;

        foreach (var character in cleaned)
        {
            var index = alphabet.IndexOf(character, StringComparison.Ordinal);
            if (index < 0)
            {
                throw new FormatException($"'{character}' is not a base32 character.");
            }

            buffer = (buffer << 5) | index;
            bits += 5;

            if (bits >= 8)
            {
                output.Add((byte)((buffer >> (bits - 8)) & 0xFF));
                bits -= 8;
            }
        }

        return [.. output];
    }
}
