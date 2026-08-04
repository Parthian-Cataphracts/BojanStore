namespace Bojan.Domain.Identity;

/// <summary>
/// What counts as an acceptable password.
/// </summary>
/// <remarks>
/// In the domain rather than in a validator because the same rule has to hold
/// for registering, for resetting, and for any later "change my password"
/// screen — three entry points that must not be able to disagree about it.
/// </remarks>
public static class PasswordPolicy
{
    public const int MinLength = 8;

    /// <summary>
    /// The ceiling exists for the server, not the customer.
    /// </summary>
    /// <remarks>
    /// PBKDF2 hashes whatever it is handed, so an unbounded field lets an
    /// anonymous caller post a multi-megabyte "password" and make the register
    /// and sign-in paths burn the full iteration count on it before rejecting
    /// anything — cheap for them, expensive here. 256 is far past any passphrase
    /// a person will type.
    /// </remarks>
    public const int MaxLength = 256;

    /// <summary>
    /// The reason the password is unacceptable, or null when it is fine.
    /// </summary>
    /// <remarks>
    /// Letters and digits only, deliberately: a symbol requirement pushes people
    /// towards "Password1!" and away from length, which is the property that
    /// actually matters.
    /// </remarks>
    public static string? Validate(string? password) => password switch
    {
        null or "" => "password-required",
        { Length: < MinLength } => "password-too-short",
        { Length: > MaxLength } => "password-too-long",
        _ when !password.Any(char.IsLetter) || !password.Any(char.IsDigit) => "password-too-simple",
        _ => null,
    };
}
