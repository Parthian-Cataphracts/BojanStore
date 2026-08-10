using Bojan.Application.Contracts;

namespace Bojan.Application.Notifications;

/// <summary>
/// Reads and writes which SMS service the shop sends through.
/// </summary>
/// <remarks>
/// In the database rather than in configuration, for the reason the payment
/// gateway's settings are: the owner enters the account in the panel, on a
/// running shop, and a code requested a minute later has to go out through it.
/// </remarks>
public interface ISmsSettingsStore
{
    /// <summary>What the panel may see — never the API key.</summary>
    Task<SmsSettingsDto> GetAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="apiKey">
    /// Null leaves the stored one alone; an explicit empty string clears it.
    /// </param>
    Task SaveAsync(SmsSettingsDto settings, string? apiKey, CancellationToken cancellationToken);
}

/// <summary>
/// Asks the configured SMS provider whether this shop's account works.
/// </summary>
/// <remarks>
/// Separate from <see cref="Auth.ISmsSender"/> for the reason the payment
/// probe is separate from the gateway: it is a diagnostic whose answer is a
/// sentence for a person, and it is the one place in the system where a
/// delivery failure should be reported rather than swallowed.
/// </remarks>
public interface ISmsProbe
{
    /// <summary>Sends one real message and reports what the provider said.</summary>
    Task<ProviderTestResult> TestAsync(string phone, CancellationToken cancellationToken);
}
