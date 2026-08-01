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

    public string Hash(string password)
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
