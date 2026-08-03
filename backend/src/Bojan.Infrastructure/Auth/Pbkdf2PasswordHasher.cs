using System.Security.Cryptography;
using Bojan.Application.Auth;

namespace Bojan.Infrastructure.Auth;

/// <summary>
/// PBKDF2-SHA256 with a random salt per password, in the
/// <c>{iterations}.{saltBase64}.{hashBase64}</c> format so the iteration
/// count can be raised later without breaking existing hashes.
/// </summary>
/// <remarks>
/// This is deliberately not the frontend's <c>hashSecret</c> — that is a
/// single unsalted SHA-256 round used to compare a mock dev password by
/// constant-time hash equality, which is fine for a value that is never
/// stored. A real password, stored for as long as the account exists, needs a
/// salt and a deliberately slow function so a leaked table cannot be
/// brute-forced with a rainbow table.
/// </remarks>
public sealed class Pbkdf2PasswordHasher : IPasswordHasher
{
    private const int Iterations = 210_000;
    private const int SaltSize = 16;
    private const int HashSize = 32;

    /// <inheritdoc cref="IPasswordHasher.PlaceholderHash"/>
    /// <remarks>
    /// Built once and shared. It hashes random bytes rather than a fixed
    /// string, so it is not a value anyone can precompute against, and no
    /// password can ever match it. This class is registered as a singleton, so
    /// the one PBKDF2 run it costs happens on first use and never again —
    /// computing it per request would put the expensive work back on the path
    /// it is meant to even out.
    /// </remarks>
    private readonly Lazy<string> _placeholder = new(
        () => HashWith(Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32))),
        LazyThreadSafetyMode.ExecutionAndPublication);

    public string PlaceholderHash => _placeholder.Value;

    public string Hash(string password) => HashWith(password);

    private static string HashWith(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);
        return $"{Iterations}.{Convert.ToBase64String(salt)}.{Convert.ToBase64String(hash)}";
    }

    public bool Verify(string password, string encoded)
    {
        var parts = encoded.Split('.', 3);
        if (parts.Length != 3 || !int.TryParse(parts[0], out var iterations))
        {
            return false;
        }

        byte[] salt;
        byte[] expectedHash;
        try
        {
            salt = Convert.FromBase64String(parts[1]);
            expectedHash = Convert.FromBase64String(parts[2]);
        }
        catch (FormatException)
        {
            return false;
        }

        var actualHash = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedHash.Length);
        return CryptographicOperations.FixedTimeEquals(actualHash, expectedHash);
    }
}
